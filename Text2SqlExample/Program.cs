using Microsoft.Extensions.Configuration;

namespace Text2SqlExample
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Text2Sql!");

            var builder = new ConfigurationBuilder()
            .AddUserSecrets<Program>(); // Use the User Secrets ID from your .csproj

            var configuration = builder.Build();

            var OpenAIAPIKey = configuration["OpenAIAPIKey"];

            string database = "AdventureWorks2017";

            string connectionString = $"Server=.\\SQLEXPRESS;Database={database};Trusted_Connection=True";

            var text2SqlClient = new Text2Sql.Text2Sql(connectionString, OpenAIAPIKey, database, "gpt-4-1106-preview");

            var openAIAPIResponse = await text2SqlClient.GenerateSqlQueryAsync("Aggregate all the products by sub category and their total list price using the following mssql database tables and corresponding fields: ");

            Console.WriteLine(openAIAPIResponse);

            var sqlResult = await text2SqlClient.ExecuteSqlQueryAsync(openAIAPIResponse);

            var tableDataResult = text2SqlClient.DapperObjectsToTableDataObject<object>(sqlResult);

            text2SqlClient.PrintTableDataToConsole(tableDataResult);
        }
    }
}