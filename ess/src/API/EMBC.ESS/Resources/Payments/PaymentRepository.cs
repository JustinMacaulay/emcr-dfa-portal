﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using EMBC.ESS.Utilities.Cas;
using EMBC.ESS.Utilities.Dynamics;
using EMBC.ESS.Utilities.Dynamics.Microsoft.Dynamics.CRM;
using Microsoft.OData.Client;

namespace EMBC.ESS.Resources.Payments
{
    internal class PaymentRepository : IPaymentRepository
    {
        private readonly IMapper mapper;
        private readonly IEssContextFactory essContextFactory;
        private readonly ICasGateway casGateway;

        private static CancellationToken CreateCancellationToken() => new CancellationTokenSource().Token;

        public PaymentRepository(IMapper mapper, IEssContextFactory essContextFactory, ICasGateway casGateway)
        {
            this.mapper = mapper;
            this.essContextFactory = essContextFactory;
            this.casGateway = casGateway;
        }

        public async Task<ManagePaymentResponse> Manage(ManagePaymentRequest request) =>
            request switch
            {
                CreatePaymentRequest r => await Handle(r, CreateCancellationToken()),
                IssuePaymentsBatchRequest r => await Handle(r, CreateCancellationToken()),
                ProcessCasPaymentReconciliationStatusRequest r => await Handle(r, CreateCancellationToken()),
                CancelPaymentRequest r => await Handle(r, CreateCancellationToken()),

                _ => throw new NotSupportedException($"type {request.GetType().Name}")
            };

        public async Task<QueryPaymentResponse> Query(QueryPaymentRequest request) =>
        request switch
        {
            SearchPaymentRequest r => await Handle(r, CreateCancellationToken()),
            GetCasPaymentStatusRequest r => await Handle(r, CreateCancellationToken()),

            _ => throw new NotSupportedException($"type {request.GetType().Name}")
        };

        private async Task<CreatePaymentResponse> Handle(CreatePaymentRequest request, CancellationToken ct)
        {
            if (request.Payment.PayeeId == null) throw new ArgumentNullException(nameof(request.Payment.PayeeId));
            if (request.Payment is InteracSupportPayment isp && !isp.LinkedSupportIds.Any()) throw new ArgumentException("Interac payment must be linked to at least one support");

            var ctx = essContextFactory.Create();

            var payment = mapper.Map<era_etransfertransaction>(request.Payment);
            payment.era_etransfertransactionid = Guid.NewGuid();
            payment.statuscode = (int)PaymentStatus.Created;
            payment.era_queueprocessingstatus = (int)QueueStatus.Pending;
            ctx.AddToera_etransfertransactions(payment);

            if (request.Payment is InteracSupportPayment ip)
            {
                // link the payment to the related supports
                foreach (var supportId in ip.LinkedSupportIds)
                {
                    var support = (await ((DataServiceQuery<era_evacueesupport>)ctx.era_evacueesupports.Where(s => s.era_name == supportId)).ExecuteAsync(ct)).SingleOrDefault();
                    if (support == null) throw new InvalidOperationException($"support id {supportId} not found");
                    ctx.AddLink(support, nameof(era_evacueesupport.era_era_evacueesupport_era_etransfertransacti), payment);
                    // set the flag on support so it would not be processed again
                    support.era_etransfertransactioncreated = true;
                    ctx.UpdateObject(support);
                }
            }
            if (payment._era_payee_value.HasValue)
            {
                // link to payee
                var payee = await ctx.contacts.ByKey(payment._era_payee_value).GetValueAsync();
                payment.era_suppliernumber = payee.era_sitesuppliernumber;
                payment.era_sitesuppliernumber = payee.era_sitesuppliernumber;
                ctx.SetLink(payment, nameof(era_etransfertransaction.era_Payee_contact), payee);
            }

            await ctx.SaveChangesAsync(ct);

            ctx.DetachAll();

            var id = (await ctx.era_etransfertransactions.ByKey(payment.era_etransfertransactionid).GetValueAsync(ct)).era_name;

            return new CreatePaymentResponse { Id = id };
        }

        private async Task<SearchPaymentResponse> Handle(SearchPaymentRequest request, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(request.ById) && string.IsNullOrEmpty(request.ByLinkedSupportId) && !request.ByStatus.HasValue)
                throw new ArgumentException("Payments query must have at least one criteria", nameof(request));

            var ctx = essContextFactory.CreateReadOnly();

            IEnumerable<era_etransfertransaction> payments = Array.Empty<era_etransfertransaction>();
            if (!string.IsNullOrEmpty(request.ByLinkedSupportId))
            {
                // search only for a single support
                var support = (await ((DataServiceQuery<era_evacueesupport>)ctx.era_evacueesupports.Where(s => s.era_name == request.ByLinkedSupportId)).GetAllPagesAsync(ct)).SingleOrDefault();
                if (support != null)
                {
                    ctx.AttachTo(nameof(EssContext.era_evacueesupports), support);
                    await ctx.LoadPropertyAsync(support, nameof(era_evacueesupport.era_era_evacueesupport_era_etransfertransacti));
                    payments = support.era_era_evacueesupport_era_etransfertransacti;
                    if (request.ByStatus.HasValue) payments = payments.Where(p => p.statuscode == (int)request.ByStatus.Value);
                    if (request.ByQueueStatus.HasValue) payments = payments.Where(tx => tx.era_queueprocessingstatus == (int)request.ByQueueStatus.Value);
                    if (!string.IsNullOrEmpty(request.ById)) payments = payments.Where(p => p.era_name == request.ById);
                }
            }
            else
            {
                // search all payments
                IQueryable<era_etransfertransaction> query = ctx.era_etransfertransactions;
                if (!string.IsNullOrEmpty(request.ById)) query = query.Where(tx => tx.era_name == request.ById);
                if (request.ByStatus.HasValue) query = query.Where(tx => tx.statuscode == (int)request.ByStatus.Value);
                if (request.ByQueueStatus.HasValue) query = query.Where(tx => tx.era_queueprocessingstatus == (int)request.ByQueueStatus.Value);
                query = query.OrderBy(q => q.createdon);
                if (request.LimitNumberOfItems.HasValue) query = query.Take(request.LimitNumberOfItems.Value);

                payments = (await query.GetAllPagesAsync(ct)).ToArray();
                await Parallel.ForEachAsync(payments, ct, async (tx, ct) =>
                {
                    ctx.AttachTo(nameof(EssContext.era_etransfertransactions), tx);
                    await ctx.LoadPropertyAsync(tx, nameof(era_etransfertransaction.era_era_evacueesupport_era_etransfertransacti), ct);
                });
            }
            ctx.DetachAll();

            return new SearchPaymentResponse { Items = mapper.Map<IEnumerable<Payment>>(payments).ToArray() };
        }

        private async Task<IssuePaymentsBatchResponse> Handle(IssuePaymentsBatchRequest request, CancellationToken ct)
        {
            var processedPayments = new ConcurrentBag<string>();
            var failedPayments = new ConcurrentBag<(string Id, Exception Error)>();

            await Parallel.ForEachAsync(request.PaymentIds, ct, async (paymentId, ct) =>
            {
                try
                {
                    await SendPaymentToCas(essContextFactory.Create(), paymentId, request.BatchId, ct);

                    processedPayments.Add(paymentId);
                }
                catch (Exception e)
                {
                    failedPayments.Add((paymentId, e));
                }
            });

            return new IssuePaymentsBatchResponse
            {
                IssuedPayments = processedPayments,
                FailedPayments = failedPayments
            };
        }

        private async Task SendPaymentToCas(EssContext ctx, string paymentId, string batch, CancellationToken ct)
        {
            var payment = (await ((DataServiceQuery<era_etransfertransaction>)ctx.era_etransfertransactions
                .Where(tx => tx.era_name == paymentId))
                .ExecuteAsync(ct))
                .SingleOrDefault();

            if (payment == null) throw new InvalidOperationException($"Payment {paymentId} not found");

            //skip any in flight payments
            if (!payment.era_queueprocessingstatus.HasValue) return;

            //load related supports
            await ctx.LoadPropertyAsync(payment, nameof(era_etransfertransaction.era_era_evacueesupport_era_etransfertransacti), ct);

            try
            {
                var validations = ValidatePaymentBeforeSendingToCas(payment).ToArray();
                if (validations.Any()) throw new CasException($"Payment {payment.era_name} validation errors: {string.Join(';', validations)}");

                // lock payment
                payment.era_queueprocessingstatus = (int)QueueStatus.Processing;
                await ctx.SaveChangesAsync(ct);

                var payee = (await ((DataServiceQuery<contact>)ctx.contacts
                    .Expand(c => c.era_ProvinceState)
                    .Expand(c => c.era_Country)
                    .Expand(c => c.era_City)
                    .Where(c => c.contactid == payment._era_payee_value))
                    .ExecuteAsync(ct))
                    .SingleOrDefault();

                if (payee.era_suppliernumber == null)
                {
                    // search CAS for supplier information
                    var supplierDetails = await casGateway.GetSupplier(payee, ct);

                    if (supplierDetails == null)
                    {
                        // create new supplier in CAS
                        supplierDetails = await casGateway.CreateSupplier(payee, ct);
                    }
                    payee.era_suppliernumber = supplierDetails.Value.SupplierNumber;
                    payee.era_sitesuppliernumber = supplierDetails.Value.SiteCode;

                    // store supplier info
                    ctx.UpdateObject(payee);
                    await ctx.SaveChangesAsync(ct);
                }

                // update CAS related fields
                var now = DateTime.UtcNow;
                payment.era_gldate = now;
                payment.era_invoicedate = now;
                payment.era_dateinvoicereceived = now;
                payment.era_suppliernumber = payee.era_suppliernumber;
                payment.era_sitesuppliernumber = payee.era_sitesuppliernumber;

                //send to CAS
                await casGateway.CreateInvoice(batch, payment, ct);
                payment.era_processingresponse = string.Empty;
                UpdatePaymentStatus(ctx, payment, PaymentStatus.Sent);
                payment.era_queueprocessingstatus = (int)QueueStatus.Pending;

                ctx.UpdateObject(payment);
                await ctx.SaveChangesAsync(ct);
            }
            catch (CasException e)
            {
                // fail the payment
                payment.era_processingresponse = e.Message;
                UpdatePaymentStatus(ctx, payment, PaymentStatus.Failed);
                payment.era_queueprocessingstatus = null;
                ctx.UpdateObject(payment);

                await ctx.SaveChangesAsync(ct);
                throw;
            }
            catch (Exception e)
            {
                // put payment back in Pending queue for retry
                payment.era_processingresponse = e.Message;
                UpdatePaymentStatus(ctx, payment, PaymentStatus.Created);
                payment.era_queueprocessingstatus = (int)QueueStatus.Pending;
                ctx.UpdateObject(payment);

                await ctx.SaveChangesAsync(ct);
                throw;
            }
        }

        private static IEnumerable<string> ValidatePaymentBeforeSendingToCas(era_etransfertransaction payment)
        {
            if (payment.statuscode != (int)PaymentStatus.Created) yield return $"Payment is in status {(PaymentStatus)payment.statuscode} - expected Pending status";
            if (!payment._era_payee_value.HasValue) yield return "Payment has no payee";
            if (!payment.era_era_evacueesupport_era_etransfertransacti.Any()) yield return "Payment is not linked to any support";
            if (string.IsNullOrEmpty(payment.era_emailaddress) && string.IsNullOrEmpty(payment.era_phonenumber)) yield return "Payment must have at least an email or a phone number";
        }

        private async Task<GetCasPaymentStatusResponse> Handle(GetCasPaymentStatusRequest request, CancellationToken ct)
        {
            var casStatuses = MapCasStatus(request.InStatus);

            var invoices = new List<InvoiceItem>();
            foreach (var status in casStatuses)
            {
                invoices.AddRange(await casGateway.QueryInvoices(status, request.ChangedFrom, request.ChangedTo, ct));
            }

            return new GetCasPaymentStatusResponse
            {
                Payments = invoices.Select(p => new CasPaymentDetails
                {
                    PaymentId = p.Invoicenumber,
                    Status = ResolveCasStatus(p.Paymentstatus),
                    StatusChangeDate = p.Paymentstatusdate.Value,
                    CasReferenceNumber = p.Paymentnumber?.ToString(),
                    StatusDescription = p.Voidreason
                })
            };
        }

        private static IEnumerable<string> MapCasStatus(CasPaymentStatus? s) =>
            s switch
            {
                CasPaymentStatus.Paid => new[] { "RECONCILED", "RECONCILED UNACCOUNTED", "CLEARED" },
                CasPaymentStatus.Pending => new[] { "NEGOTIABLE" },
                CasPaymentStatus.Failed => new[] { "VOIDED" },

                _ => new[] { string.Empty }
            };

        private static CasPaymentStatus ResolveCasStatus(string? s) =>
            s.ToUpperInvariant() switch
            {
                "RECONCILED" => CasPaymentStatus.Paid,
                "CLEARED" => CasPaymentStatus.Paid,
                "RECONCILED UNACCOUNTED" => CasPaymentStatus.Paid,
                "VOIDED" => CasPaymentStatus.Failed,
                "NEGOTIABLE" => CasPaymentStatus.Pending,

                _ => throw new NotImplementedException($"CAS payment status {s}")
            };

        private static PaymentStatus ResolvePaymentStatus(CasPaymentStatus status) =>
            status switch
            {
                CasPaymentStatus.Paid => PaymentStatus.Paid,
                CasPaymentStatus.Pending => PaymentStatus.Issued,
                CasPaymentStatus.Failed => PaymentStatus.Failed,

                _ => throw new NotImplementedException($"CAS payment status {status}")
            };

        private async Task<ProcessCasPaymentReconciliationStatusResponse> Handle(ProcessCasPaymentReconciliationStatusRequest request, CancellationToken ct)
        {
            var ctx = essContextFactory.Create();

            var paymentId = request.CasPaymentDetails.PaymentId;
            var payment = await ctx.era_etransfertransactions.Where(t => t.era_name == paymentId).SingleOrDefaultAsync();
            if (payment == null) throw new InvalidOperationException($"payment {paymentId} not found");

            // guard for current payment status
            if (!new[] { PaymentStatus.Sent, PaymentStatus.Issued }.Contains((PaymentStatus)payment.statuscode))
                throw new InvalidOperationException($"cannot reconcile payment {paymentId} as it in status {(PaymentStatus)payment.statuscode}");

            // skip in flight payments
            //if (payment.era_queueprocessingstatus == (int)QueueStatus.Processing)

            // guard against changing a later status
            if (payment.era_casresponsedate > request.CasPaymentDetails.StatusChangeDate) throw new InvalidOperationException($"Payment {paymentId} was already reconciled on {payment.era_casresponsedate}");

            payment.era_casresponsedate = request.CasPaymentDetails.StatusChangeDate;
            payment.era_casreferencenumber = request.CasPaymentDetails.CasReferenceNumber;
            payment.era_processingresponse = request.CasPaymentDetails.StatusDescription;
            UpdatePaymentStatus(ctx, payment, ResolvePaymentStatus(request.CasPaymentDetails.Status));

            ctx.UpdateObject(payment);

            await ctx.SaveChangesAsync(ct);

            return new ProcessCasPaymentReconciliationStatusResponse { };
        }

        private async Task<CancelPaymentResponse> Handle(CancelPaymentRequest request, CancellationToken ct)
        {
            var ctx = essContextFactory.Create();

            var paymentId = request.PaymentId;
            var payment = await ctx.era_etransfertransactions.Where(t => t.era_name == paymentId).SingleOrDefaultAsync();
            if (payment == null) throw new InvalidOperationException($"payment {paymentId} not found");

            // guard for current payment status
            if (!new[] { PaymentStatus.Failed }.Contains((PaymentStatus)payment.statuscode))
                throw new InvalidOperationException($"cannot cancel payment {paymentId} as it in status {(PaymentStatus)payment.statuscode}");

            UpdatePaymentStatus(ctx, payment, PaymentStatus.Cancelled);

            payment.era_processingresponse = request.Reason;
            await ctx.SaveChangesAsync(ct);
            ctx.UpdateObject(payment);
            return new CancelPaymentResponse { };
        }

        private static void UpdatePaymentStatus(EssContext ctx, era_etransfertransaction payment, PaymentStatus status)
        {
            switch (status)
            {
                case PaymentStatus.Created:
                case PaymentStatus.Sent:
                case PaymentStatus.Failed:
                case PaymentStatus.Issued:
                    ctx.ActivateObject(payment, (int)status);
                    break;

                case PaymentStatus.Paid:
                case PaymentStatus.Cancelled:
                    ctx.DeactivateObject(payment, (int)status);
                    break;

                default:
                    throw new NotImplementedException($"Update payment status to {status}");
            }
        }
    }
}
