// See https://aka.ms/new-console-template for more information
using PipelineChunker;
using System.Collections;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Data.SqlTypes;
using Driver;

static class Program {
    static bool testErrors = true;
    static void Main(string[] args) {
        Driver2.TheMain(args);
    }
}