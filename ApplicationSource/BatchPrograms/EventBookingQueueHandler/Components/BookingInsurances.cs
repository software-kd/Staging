using Alphareds.Module.Common;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBookingQueueHandler.Components
{
    class InsuranceService
    {
        public Logger Logger { get; set; }

        public InsuranceService(Logger _logger)
        {
            Logger = _logger;
        }

        public ProductReserve.BookingRespond ConfirmInsuranceQuotation(BookingInsurance bookingInsurance, string superPNRNo)
        {
            var res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetTravelPolicy(bookingInsurance, superPNRNo);
            List<Tuple<string, bool>> bookStatus = new List<Tuple<string, bool>>();
            foreach (var x in res)
            {
                try
                {
                    var policyDetails = x.Body?.GetTravelPolicyResponse?.ACORD?.InsuranceSvcRs?.PersPkgPolicyAddRs;
                    var insuredPeopleName = policyDetails.InsuredOrPrincipal?.GeneralPartyInfo?.NameInfo[0].PersonName;
                    var paxInfo = bookingInsurance.InsurancePaxes.FirstOrDefault(p =>
                    p.GivenName.Trim() == insuredPeopleName.GivenName.Trim() && p.SurName.Trim() == insuredPeopleName.Surname.Trim());

                    var paxPersons = policyDetails.InsuredOrPrincipal?.GeneralPartyInfo?.NameInfo;
                    //int paxIdCheck = 0;

                    if (x.Errors != null && policyDetails != null
                        && policyDetails.MsgStatus.MsgStatusCd != "Success")
                    {
                        //paxInfo.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;
                        //foreach (var paxPeople in paxPersons)
                        //{
                        //    var paxInformation = bookingInsurance.InsurancePaxes.FirstOrDefault(p => p.GivenName.Trim() == paxPeople.PersonName.GivenName.Trim() && p.SurName.Trim() == paxPeople.PersonName.Surname.Trim() && p.PaxID != paxIdCheck);
                        //    //paxInformation.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        //    //paxInformation.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;
                        //    paxIdCheck = paxInformation.PaxID;
                        //}

                        bookingInsurance.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        bookingInsurance.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;

                        Logger.Error("Reserve Insurance Failed on SuperPNRNo " + superPNRNo + " , Named: " + paxInfo.GivenName + " " + paxInfo.SurName);
                        bookStatus.Add(new Tuple<string, bool>("Reserve Insurance Failed - " + policyDetails.MsgStatus.MsgStatusCd + Environment.NewLine + Environment.NewLine +
                        insuredPeopleName?.GivenName?.Trim() + " " + insuredPeopleName?.Surname?.Trim(), false));
                    }
                    else if (policyDetails.MsgStatus.MsgStatusCd == "Success")
                    {
                        // TODO: Update reserveed information to dbContext
                        //paxInfo.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        //paxInfo.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;

                        //foreach (var paxPeople in paxPersons)
                        //{
                        //    var paxInformation = bookingInsurance.InsurancePaxes.FirstOrDefault(p => p.GivenName.Trim() == paxPeople.PersonName.GivenName.Trim() && p.SurName.Trim() == paxPeople.PersonName.Surname.Trim() && p.PaxID != paxIdCheck);
                        //    //paxInformation.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        //    //paxInformation.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;
                        //    paxIdCheck = paxInformation.PaxID;
                        //}

                        bookingInsurance.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        bookingInsurance.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;

                        bookStatus.Add(new Tuple<string, bool>(insuredPeopleName.GivenName ?? "", true));
                    }
                    else
                    {
                        //paxInfo.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;
                        //foreach (var paxPeople in paxPersons)
                        //{
                        //    var paxInformation = bookingInsurance.InsurancePaxes.FirstOrDefault(p => p.GivenName.Trim() == paxPeople.PersonName.GivenName.Trim() && p.SurName.Trim() == paxPeople.PersonName.Surname.Trim() && p.PaxID != paxIdCheck);
                        //    //paxInformation.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        //    //paxInformation.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;
                        //    paxIdCheck = paxInformation.PaxID;
                        //}

                        bookingInsurance.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        bookingInsurance.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;

                        bookStatus.Add(new Tuple<string, bool>("Unknow status - " + policyDetails.MsgStatus.MsgStatusCd + Environment.NewLine + Environment.NewLine +
                        insuredPeopleName?.GivenName?.Trim() + " " + insuredPeopleName?.Surname?.Trim(), false));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "An error occured when attemp to get insurance policy  no. " + DateTime.Today.ToLoggerDateTime());
                    bookStatus.Add(new Tuple<string, bool>("Catch ex when ConfirmInsuranceQuotation on" + superPNRNo, false));
                }
            }

            return new ProductReserve.BookingRespond
            {
                SuperPNRNo = superPNRNo,
                BatchBookResult = bookStatus.All(x => x.Item2) ? ProductReserve.BookResultType.AllSuccess
                : bookStatus.All(x => !x.Item2) ? ProductReserve.BookResultType.AllFail
                : ProductReserve.BookResultType.PartialSuccess,
            };
        }

    }
}
