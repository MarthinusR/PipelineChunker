using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Driver {
    public class Utilities {
        static bool IsInitExecFlattenedStoreProcAsDataSetBatcher = false;
        static Dictionary<string, Func<DataTable, DataSet>> preparedMap = new Dictionary<string, Func<DataTable, DataSet>>();
        public static void Init(SqlConnectionStringBuilder connectionStringBuilder) {
            using (SqlConnection conn = new SqlConnection(connectionStringBuilder.ConnectionString)) {
                conn.Open();
                string sql = $"""
                if object_id(N'tblExample', N'U') is null
                begin
                    create table tblExample (
                        id int
                    )
                    select 1
                end
                """;
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        var result = reader.GetInt32(0);
                        if (result == 1) {
                            Debug.WriteLine(" --- Created tblExample");
                        }
                    }
                }
                sql = $"""
                if object_id(N'usp_Example') is null
                begin
                	exec('create or alter procedure usp_Example
                		@a int,
                		@b int
                	as begin
                		(select (@a + @b))
                	end')
                	select 1
                end
                """;
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        var result = reader.GetInt32(0);
                        if (result == 1) {
                            Debug.WriteLine(" --- Created usp_Example");
                        }
                    }
                }
            }
        }
        public static DataSet ExecFlattenedStoreProcAsDataSetBatcher(SqlCommand cmd, string storeProc, DataTable parameterTable) {
            DataSet dataSet;
            if (!IsInitExecFlattenedStoreProcAsDataSetBatcher) {
                IsInitExecFlattenedStoreProcAsDataSetBatcher = true;
                cmd.CommandText = $"""                        
            create or alter procedure usp_FlattenedStoreProcAsDataSetBatcher
            	@proc varchar(255)
            as begin
            	declare @unique varchar(255) = 'Batched_c82473d65be546d8b965897a6ac039c0_'
            	declare @outProcName varchar(255) = 'usp_' + @unique + @proc
            	declare @inputTypeName varchar(255) = @unique + @proc + 'UserType'

            	declare @tblInfo as table(
            		name varchar(255),
            		type varchar(255)
            	)
            	-- how-to-get-stored-procedure-parameters-details https://stackoverflow.com/a/41330791
            	insert into @tblInfo (name, type) select parameters.name, t.name from sys.parameters 
            		inner join sys.procedures p on parameters.object_id = p.object_id
            		inner join sys.types t on parameters.system_type_id = t.system_type_id AND parameters.user_type_id = t.user_type_id
            		where p.name = 'usp_Example'

            	DECLARE @Parameters VARCHAR(8000) 
            	DECLARE @AssignedParameters VARCHAR(8000) 
            	DECLARE @ProcedureParameters VARCHAR(8000) 
            	DECLARE @TableParameters VARCHAR(8000) 
            	SELECT @TableParameters = COALESCE(@TableParameters + ', ', '') + REPLACE(name, '@', '') + ' ' + 
            		case
            			when type = 'varchar' then (type + '(MAX)')
            			else type
            		end
            	from @tblInfo
            	SELECT @ProcedureParameters = COALESCE(@ProcedureParameters + ' declare ', 'declare ') + name + ' ' + 
            		case
            			when type = 'varchar' then (type + '(MAX)')
            			else type
            		end
            	from @tblInfo
            	SELECT @Parameters = COALESCE(@Parameters + ', ', '') + name
            	from @tblInfo
            	SELECT @AssignedParameters = COALESCE(@AssignedParameters + ', ', '') + name + '=' + name
            	from @tblInfo

            	exec(
            	'if object_id(''' + @outProcName + ''') is not null
            	begin
            		drop procedure ' + @outProcName + '
            	end
            	')
            	exec(
            	'if type_id(''' + @inputTypeName + ''') is not null
            	begin
            		drop type ' + @inputTypeName + '
            	end
            	create type ' + @inputTypeName + ' as table(
            		' + @TableParameters + '
            	)')

            	exec(
            	'create or alter procedure ' + @outProcName + '
            		@input ' + @inputTypeName + ' readonly
            	as begin
            		' + @ProcedureParameters + '
            		declare #cursor cursor local dynamic forward_only read_only for (select * from @input)
            		OPEN #cursor
            		do_loop:
            			fetch next from #cursor into ' + @Parameters + '
            			if @@FETCH_STATUS <> 0
            				goto exit_do_loop
            			begin try
            				exec usp_Example ' + @AssignedParameters + '
            			end try begin catch
            				(select ERROR_MESSAGE(), ERROR_SEVERITY(), ERROR_STATE())
            			end catch
            		goto do_loop
            		exit_do_loop:
            	end')
            end            
            """;
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Clear();
                cmd.ExecuteNonQuery();
            }

            if (!preparedMap.TryGetValue(storeProc, out var process)) {
                cmd.CommandText = $"exec usp_FlattenedStoreProcAsDataSetBatcher '{storeProc}'";
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Clear();
                cmd.ExecuteNonQuery();
                preparedMap[storeProc] = process = (dataTable) => {
                    Thread.Sleep(30);
                    cmd.CommandText = $"usp_Batched_c82473d65be546d8b965897a6ac039c0_{storeProc}";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Clear();
                    SqlParameter parameter = cmd.Parameters.AddWithValue("@input", dataTable);
                    parameter.SqlDbType = SqlDbType.Structured;
                    parameter.TypeName = $"Batched_c82473d65be546d8b965897a6ac039c0_{storeProc}UserType";
                    DataSet set = new DataSet();
                    new SqlDataAdapter(cmd).Fill(set);
                    return set;
                };
            }
            return process(parameterTable);
        }
    }
}
