using Azure;
using Azure.AI.OpenAI;
using ConsoleTables;
using Dapper;
using System.Data.SqlClient;

namespace Text2Sql
{
    public class Text2Sql
    {
        private readonly string _connectionString;
        private readonly string _openAIAPIKey;
        private readonly string _database;
        private readonly string _model;

        public Text2Sql(string connectionString, string openAIAPIKey, string dataBase, string model = "gpt-4-1106-preview")
        {
            _connectionString = connectionString;
            _openAIAPIKey = openAIAPIKey;
            _database = dataBase;
            _model = model;
        }

        public async Task<string> GenerateSqlQueryAsync(string input, bool generateSQL = true)
        {
            OpenAIClient client = new OpenAIClient(_openAIAPIKey);

            List<List<string>> listOfLists = new List<List<string>>();

            string allTablesAndFieldsAsString = "";

            using (var connection = new SqlConnection(_connectionString))
            {
                var sqlAllTables = $"SELECT TABLE_SCHEMA + '.' + TABLE_NAME AS FullTableName FROM {_database}.INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';";
                var tables = await connection.QueryAsync(sqlAllTables);

                foreach (var table in tables)
                {
                    var oneCompleteTableWithFields = new List<string>();

                    var tableHeaderData = $"Table name: {table.FullTableName} Fields: ";

                    allTablesAndFieldsAsString = allTablesAndFieldsAsString + tableHeaderData;
                    oneCompleteTableWithFields.Add(tableHeaderData);

                    var tableName = table.FullTableName.Substring(table.FullTableName.IndexOf('.') + 1);

                    var sqlAllTableData = $"SELECT COLUMN_NAME,DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{tableName}'";
                    var columns = await connection.QueryAsync(sqlAllTableData);

                    foreach (var column in columns)
                    {
                        var tabelAndFieldData = $"{column.COLUMN_NAME},{column.DATA_TYPE};";

                        allTablesAndFieldsAsString = allTablesAndFieldsAsString + tabelAndFieldData;
                        oneCompleteTableWithFields.Add(tabelAndFieldData);
                    }
                    allTablesAndFieldsAsString = allTablesAndFieldsAsString + "\n";
                    listOfLists.Add(oneCompleteTableWithFields);
                }
            }

            Console.WriteLine(allTablesAndFieldsAsString);

            Response<ChatCompletions> chatResponses;

            if (generateSQL)
            {
                // Chat request.
                chatResponses = await client.GetChatCompletionsAsync(new ChatCompletionsOptions()
                {
                    DeploymentName = _model, // assumes a matching model deployment or model name
                    Temperature = 0.0f,
                    Messages = { new ChatMessage {
                    Role = ChatRole.User,
                    Content = $"{input}: {allTablesAndFieldsAsString} and then generate a Sql query and then put the query between ```sql and ```"

                } }
                });
            }
            else
            {
                // Chat request.
                chatResponses = await client.GetChatCompletionsAsync(new ChatCompletionsOptions()
                {
                    DeploymentName = _model, // assumes a matching model deployment or model name
                    Temperature = 0.0f,
                    Messages = { new ChatMessage {
                    Role = ChatRole.User,
                    Content = $"{input}: {allTablesAndFieldsAsString}"

                } }
                });
            }
            

            return chatResponses.Value.Choices[0].Message.Content;
        }

        public async Task<IEnumerable<object>> ExecuteSqlQueryAsync(string input)
        {
            var extractedSQLQuery = ExtractSQLQuery(input);

            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync(extractedSQLQuery);
                return result;
            }
        }

        private string ExtractSQLQuery(string text)
        {
            string startDelimiter = "```sql";
            string endDelimiter = "```";

            int startIndex = text.IndexOf(startDelimiter);
            if (startIndex == -1)
            {
                return "No SQL query found.";
            }
            startIndex += startDelimiter.Length;

            int endIndex = text.IndexOf(endDelimiter, startIndex);
            if (endIndex == -1)
            {
                return "No closing delimiter found.";
            }

            string sqlQuery = text.Substring(startIndex, endIndex - startIndex).Trim();
            return sqlQuery;
        }

        public TableDataObject DapperObjectsToTableDataObject<T>(IEnumerable<object> dapperRowObjects)
        {
            var response = new TableDataObject();
            response.Headers = new List<string>();
            response.Data = new List<List<string>>();

            for (int x = 0; x < dapperRowObjects.Count(); x++)
            {
                response.Data.Add(new List<string>());
            }

            if (dapperRowObjects.First() is IDictionary<string, object> dictionaryKeys)
            {
                foreach (var key in dictionaryKeys.Keys)
                {
                    response.Headers.Add(key);
                }
            }

            int i = 0;
            foreach (var itdapperRowObject in dapperRowObjects)
            {
                if (itdapperRowObject is IDictionary<string, object> dictionary)
                {

                    foreach (var kvp in dictionary)
                    {
                        // Console.WriteLine($"{kvp.Key} = {kvp.Value}");
                        response.Data[i].Add(kvp.Value.ToString());
                    }
                }
                else
                {
                    Console.WriteLine("Item cannot be treated as a dictionary.");
                }
                i++;
            }

            return response;
        }

        public void PrintTableDataToConsole(TableDataObject dbData)
        {
            var table = new ConsoleTable(dbData.Headers.ToArray());

            foreach (var item in dbData.Data)
            {
                table.AddRow(item.ToArray());
            }

            table.Write();
        }
    }
}