using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GLUploadIssue_ACU2023R2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Write("Enter Acumatica 2023R2 URL: ");
            var url = Console.ReadLine();
            Console.Write("Tenant: ");
            var tenant = Console.ReadLine();
            Console.Write("Username: ");
            var username = Console.ReadLine();
            Console.Write("Password: ");
            var password = Console.ReadLine();

            var tran = new GLTransaction
            {
                TransactionDate = new DateTime(2016, 01, 01),
                Description = "Custom fields test",
                Hold = false,
                Release = false
            };

            var line1 = new GLTransactionDetail
            {
                Account = "41000",
                Subaccount = "ELE-000",
                DebitAmount = 1000,
                TransactionDescription = "Line 1 description",
                CustomColumns = new Dictionary<string, object>
                    { { "UsrTestField", "ABC" }, { "UsrTestDecimal", 123d } }
            };
            tran.Details.Add(line1);

            var line2 = new GLTransactionDetail
            {
                Account = "41000",
                Subaccount = "ELE-000",
                CreditAmount = 1000,
                TransactionDescription = "Line 2 description",
                CustomColumns = new Dictionary<string, object>
                    { { "UsrTestField", "123" }, { "UsrTestDecimal", 456.78d } }
            };
            tran.Details.Add(line2);

            TestUploadAsync(
                new AcumaticaConnection
                {
                    Url = url,
                    Tenant = tenant,
                    Username = username,
                    Password = password
                },
                new[] { tran }).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.Write("Program finished. Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task TestUploadAsync(AcumaticaConnection connection, ICollection<GLTransaction> transactions)
        {
            var uploadService = new GLTransactionUploadScreenBasedService(new SoapLoginService());

            foreach (var transaction in transactions)
            {
                await uploadService.UploadAsync(
                    connection,
                    transaction);
            }

            foreach (var transaction in transactions)
            {
                await uploadService.DeleteBatchAsync(
                    connection,
                    transaction.BatchNbr);
            }
        }
    }
}
