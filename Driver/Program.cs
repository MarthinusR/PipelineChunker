// See https://aka.ms/new-console-template for more information
using PipelineChunker;
using System.Collections;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Data.SqlTypes;

static class Program {
    static bool IsInitExecFlattenedStoreProcAsDataSetBatcher = false;
    static Dictionary<string, Func<DataTable, DataSet>> preparedMap = new Dictionary<string, Func<DataTable, DataSet>>();
    static DataSet ExecFlattenedStoreProcAsDataSetBatcher(SqlCommand cmd, string storeProc, DataTable parameterTable) {
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

        if(!preparedMap.TryGetValue(storeProc, out var process)) {
            cmd.CommandText = $"exec usp_FlattenedStoreProcAsDataSetBatcher '{storeProc}'";
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Clear();
            cmd.ExecuteNonQuery();
            preparedMap[storeProc] = process = (dataTable) => {
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
    static void Main(string[] args) {
        var pipe = new Pipeline();

        var connectionStringBuilder = new SqlConnectionStringBuilder();
        connectionStringBuilder.ConnectionString = "Server=localhost\\SQLEXPRESS;Database=PipelineChunker;Trusted_Connection=True;Encrypt=False;";
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

        using (SqlConnection conn = new SqlConnection(connectionStringBuilder.ConnectionString))
        using (SqlCommand cmd = conn.CreateCommand()) {
            conn.Open();
            var TestConduit1Open = false;
            var TestConduit1Channeling = false;
            do {
                for (int i = 0; i < 100; i++) {
                    pipe.Channel<TestConduit1>(
                        Origin: () => {
                            return Pipelined1(cmd, i, pipe);
                        },
                        Operation: (conduit) => {
                            Debug.WriteLine($"Final {conduit}");
                            // invoked when all enumerators terminate.
                        });
                }
                pipe.Flush<TestConduit1>();
                pipe.GetChannelState<TestConduit1>(ref TestConduit1Open, ref TestConduit1Channeling);
            } while (pipe.IsOpen);
        }
    }


    internal class TestConduit1 : IConduit {
        int _id;
        public int sum = 0;
        int IConduit.Id => _id;
        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState channelItem => _channelItem;
        public IEnumerator<TestConduit1> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
        void IConduit.Initialize(IPipeline conduitOwner) {
            TestConduit1 self = this;
            conduitOwner.Bind(ref self, ref _id, ref _channelItem);
        }
        public class Phase1 : Phase {
            public SqlCommand cmd;
            public int value;
            public Phase1() { }
            override public IEnumerable<KeyValuePair<string, DataTable>> parameterTables {
                get {
                    var table = new DataTable();
                    table.Columns.Add("@a");
                    table.Columns.Add("@b");
                    var theParameterTables = new KeyValuePair<string, DataTable>[]{
                        new KeyValuePair<string, DataTable>("@input", table)
                    };
                    return theParameterTables;
                }
            }

            override public DataSet Execute(IEnumerable<KeyValuePair<String, DataTable>> parameterTables) {
                return ExecFlattenedStoreProcAsDataSetBatcher(cmd, "usp_Example", parameterTables.First().Value);
            }
        }
        public class Phase2 : Phase {
            public SqlCommand cmd;
            override public IEnumerable<KeyValuePair<string, DataTable>> parameterTables {
                get {
                    var table = new DataTable();
                    table.Columns.Add("@a");
                    table.Columns.Add("@b");
                    var theParameterTables = new KeyValuePair<string, DataTable>[]{
                        new KeyValuePair<string, DataTable>("@input", table)
                    };
                    return theParameterTables;
                }
            }

            public override DataSet Execute(IEnumerable<KeyValuePair<string, DataTable>> parameterTables) {
                return ExecFlattenedStoreProcAsDataSetBatcher(cmd, "usp_Example", parameterTables.First().Value);
            }
        }
    }
    internal class TestConduit2 : IConduit {
        int _id;
        int IConduit.Id => _id;
        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState channelItem => _channelItem;
        public IEnumerator<TestConduit2> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
        void IConduit.Initialize(IPipeline conduitOwner) {
            TestConduit2 self = this;
            conduitOwner.Bind(ref self, ref _id, ref _channelItem);
        }
    }

    static IEnumerable<TestConduit1> Pipelined1(SqlCommand cmd, int i, Pipeline owner) {
        var conduit = new TestConduit1();
        // first yield will initialize the conduit.
        yield return conduit;
        int someValue = 0;
        var phase1 = conduit.channelItem.Chunk<TestConduit1.Phase1>(
            conduit,
            (DataRow row) => {
                row["@a"] = i;
                row["@b"] = i;
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0];
            });
        phase1.cmd = cmd;
        yield return conduit;

        Debug.WriteLine($"Phase1-computed: {someValue}  -- {i}");
        conduit.sum += someValue;

        conduit.channelItem.Chunk<TestConduit1.Phase2>(
            conduit,
            (DataRow row) => {
                row["@a"] = 10 * i;
                row["@b"] = 10 * i;
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0];
            });
        yield return conduit;

        Debug.WriteLine($"Phase2-computed: {someValue}  -- {i}");
        conduit.sum += someValue;

        conduit.channelItem.Chunk<TestConduit1.Phase1>(
            conduit,
            (DataRow row) => {
                row["@a"] = -1;
                row["@b"] = 1;
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0];
            });
        Debug.WriteLine($"Phase3-computed: {someValue}  -- {i}");
        conduit.sum += someValue;
        yield return conduit;
    }
    static IEnumerable<TestConduit2> Pipelined2(int i, Pipeline owner) {
        var conduit = new TestConduit2();
        // first yield will initialize the conduit.
        yield return conduit;

        yield return conduit;
    }

}