using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Velixo.Reports.AcumaticaSoap;
using Action = Velixo.Reports.AcumaticaSoap.Action;

namespace GLUploadIssue_ACU2023R2
{
    public class GLTransactionUploadScreenBasedService
    {
        private static IFormatProvider FormatProvider { get; } = CultureInfo.CreateSpecificCulture("en-US");

        private const string _glTransactionScreenID = "GL301000";
        private const string _headerView = "BatchModule";
        public const string DetailView = "GLTranModuleBatNbr";

        private readonly SoapLoginService _soapLoginService;

        public GLTransactionUploadScreenBasedService(SoapLoginService soapLoginService)
        {
            _soapLoginService = soapLoginService;
        }

        public async Task UploadAsync(AcumaticaConnection connection, GLTransaction transaction)
        {
            await _soapLoginService.LoginAsync(
                connection,
                async screen =>
                {
                    IEnumerable<Command> headerCommands = GetHeaderCommands(transaction);

                    // Lines -- filter out anything that's zero or that rounds to 0.
                    // As per https://en.wikipedia.org/wiki/ISO_4217 there are countries with more than 2 decimals for their currency,
                    // but let's not care about that for now.
                    // This could also be an issue with crypto currencies if people start posting such transactions using GL writeback.
                    // Note: GL Budget also does the same rounding with period amounts
                    // -
                    GLTransactionDetail[] details = transaction.Details
                        .Where(l => decimal.Round(l.DebitAmount.GetValueOrDefault(), 2) != 0 || decimal.Round(l.CreditAmount.GetValueOrDefault(), 2) != 0)
                        .ToArray();

                    try
                    {
                        IEnumerable<Command> commands = headerCommands
                            .Concat(details.SelectMany(GetTransactionDetailCommands))
                            .Concat(GetSaveAndReleaseCommands(transaction));

                        Content[] results = await screen.SubmitAsync(_glTransactionScreenID, commands.ToArray());

                        if (results.Length == 1)
                        {
                            transaction.BatchNbr = results[0].Containers.Single(c => c?.Name == "BatchSummary")
                                .Fields.Single(f => f?.FieldName == "BatchNbr").Value;
                        }

                        return results;
                    }
                    catch (FaultException ex)
                    {
                        Console.WriteLine(ex);

                        if (!string.IsNullOrEmpty(transaction.BatchNbr))
                        {
                            await DeleteBatchAsync(connection, transaction.BatchNbr);
                        }

                        throw ex;
                    }
                });
        }

        public async Task DeleteBatchAsync(AcumaticaConnection connection, string batchNbr)
        {
            await _soapLoginService.LoginAsync(
                connection,
                async screen =>
                {
                    var commands = new List<Command>
                    {
                        new Key
                        {
                            ObjectName = _headerView, FieldName = "Module",
                            Value = $"=[{_headerView}.Module]"
                        },
                        new Key
                        {
                            ObjectName = _headerView, FieldName = "BatchNbr",
                            Value = $"=[{_headerView}.BatchNbr]"
                        },
                        new Action { ObjectName = _headerView, FieldName = "Cancel" },
                        new Value
                        {
                            ObjectName = _headerView, FieldName = "BatchNbr",
                            Value = batchNbr, Commit = true
                        },
                        new Action { ObjectName = _headerView, FieldName = "Delete" }
                    };

                    return await screen.SubmitAsync(
                        _glTransactionScreenID,
                        commands.ToArray());
                });
        }

        private static IEnumerable<Command> GetHeaderCommands(GLTransaction transaction)
        {
            //Setup keys and add
            yield return new Key { ObjectName = _headerView, FieldName = "Module", Value = $"=[{_headerView}.Module]" };
            yield return new Key { ObjectName = _headerView, FieldName = "BatchNbr", Value = $"=[{_headerView}.BatchNbr]" };
            yield return new Action { ObjectName = _headerView, FieldName = "Cancel" };
            yield return new Value { ObjectName = _headerView, FieldName = "BatchNbr", Value = "<NEW>", Commit = true };

            //Header
            yield return new Value
            {
                ObjectName = _headerView,
                FieldName = "Hold",
                Value = transaction.Hold.ToString(FormatProvider)
            };

            if (!string.IsNullOrEmpty(transaction.Branch))
            {
                yield return new Value { ObjectName = _headerView, FieldName = "BranchID", Value = transaction.Branch };
            }

            if (!string.IsNullOrEmpty(transaction.Ledger))
            {
                yield return new Value { ObjectName = _headerView, FieldName = "LedgerID", Value = transaction.Ledger };
            }

            if (!string.IsNullOrEmpty(transaction.Currency))
            {
                yield return new Value { ObjectName = _headerView, FieldName = "CuryID", Value = transaction.Currency };
            }

            if (transaction.TransactionDate != null)
            {
                yield return new Value
                {
                    ObjectName = _headerView,
                    FieldName = "DateEntered",
                    Value = transaction.TransactionDate.Value.ToString(FormatProvider)
                };
            }

            if (!string.IsNullOrEmpty(transaction.PostPeriod))
            {
                yield return new Value
                { ObjectName = _headerView, FieldName = "FinPeriodID", Value = transaction.PostPeriod };
            }

            if (!string.IsNullOrEmpty(transaction.Description))
            {
                yield return new Value
                { ObjectName = _headerView, FieldName = "Description", Value = transaction.Description };
            }

            yield return new Value
            {
                ObjectName = _headerView,
                FieldName = "AutoReverse",
                Value = transaction.AutoReverse.ToString(FormatProvider)
            };
            if (transaction.CreateTaxTransactions)
            {
                yield return new Value
                {
                    ObjectName = _headerView,
                    FieldName = "CreateTaxTrans",
                    Value = transaction.CreateTaxTransactions.ToString(FormatProvider)
                };
            }

            if (transaction.SkipTaxAmountValidation)
            {
                yield return new Value
                {
                    ObjectName = _headerView,
                    FieldName = "SkipTaxValidation",
                    Value = transaction.SkipTaxAmountValidation.ToString(FormatProvider)
                };
            }
        }

        private static IEnumerable<Command> GetSaveAndReleaseCommands(GLTransaction transaction)
        {
            // We want the RefNbr to be returned
            // -
            yield return new Field { ObjectName = _headerView, FieldName = "BatchNbr" };

            // Save and submit
            // -
            yield return transaction.Release && !transaction.Hold
                ? new Action { ObjectName = _headerView, FieldName = "Release" }
                : new Action { ObjectName = _headerView, FieldName = "Save" };
        }

        private static IEnumerable<Command> GetTransactionDetailCommands(GLTransactionDetail line)
        {
            yield return new NewRow { ObjectName = DetailView };

            if (!string.IsNullOrEmpty(line.Branch))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "BranchID", Value = line.Branch };
            }

            yield return new Value { ObjectName = DetailView, FieldName = "AccountID", Value = line.Account };

            if (!string.IsNullOrEmpty(line.Subaccount))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "SubID", Value = line.Subaccount };
            }

            if (!string.IsNullOrEmpty(line.Project))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "ProjectID", Value = line.Project };
            }

            if (!string.IsNullOrEmpty(line.ProjectTask))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "TaskID", Value = line.ProjectTask };
            }

            if (!string.IsNullOrEmpty(line.CostCode))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "CostCodeID", Value = line.CostCode };
            }

            yield return new Value
            {
                ObjectName = DetailView,
                FieldName = "NonBillable",
                Value = line.NonBillable.ToString(FormatProvider)
            };

            if (!string.IsNullOrEmpty(line.ReferenceNbr))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "RefNbr", Value = line.ReferenceNbr };
            }

            if (line.Qty.GetValueOrDefault() != 0)
            {
                yield return new Value
                {
                    ObjectName = DetailView,
                    FieldName = "Qty",
                    Value = line.Qty.Value.ToString(FormatProvider)
                };
            }

            if (!string.IsNullOrEmpty(line.Uom))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "UOM", Value = line.Uom };
            }

            if (!string.IsNullOrEmpty(line.TransactionDescription))
            {
                yield return new Value
                { ObjectName = DetailView, FieldName = "TranDesc", Value = line.TransactionDescription };
            }

            if (!string.IsNullOrEmpty(line.TaxCategory))
            {
                yield return new Value
                { ObjectName = DetailView, FieldName = "TaxCategoryID", Value = line.TaxCategory };
            }

            if (!string.IsNullOrEmpty(line.TaxID))
            {
                yield return new Value { ObjectName = DetailView, FieldName = "TaxID", Value = line.TaxID };
            }

            if (line.CustomColumns != null)
            {
                foreach (KeyValuePair<string, object> customColumn in line.CustomColumns)
                {
                    var value = customColumn.Value switch
                    {
                        double doubleValue => doubleValue.ToString(FormatProvider),
                        DateTime dateTimeValue => dateTimeValue.ToString(FormatProvider),
                        string stringValue => stringValue,
                        _ => null
                    };

                    yield return new Value { ObjectName = DetailView, FieldName = customColumn.Key, Value = value };
                }
            }

            if (line.DebitAmount.GetValueOrDefault() != 0)
            {
                yield return new Value
                {
                    ObjectName = DetailView,
                    FieldName = "CuryDebitAmt",
                    Value = line.DebitAmount.Value.ToString(FormatProvider),
                    Commit = true
                };
            }
            else if (line.CreditAmount.GetValueOrDefault() != 0)
            {
                yield return new Value
                {
                    ObjectName = DetailView,
                    FieldName = "CuryCreditAmt",
                    Value = line.CreditAmount.Value.ToString(FormatProvider),
                    Commit = true
                };
            }
        }
    }
}