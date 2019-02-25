using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model;
using Mayflower.Controllers;
using Alphareds.Module.Model.Database;
using System.Linq.Expressions;

namespace MayflowerBookingUnitTest
{
    [TestClass]
    public class CommonTesting
    {
        SearchFlightResultViewModel flightModel = InitializeTestingModel.FlightTestingModel.SearchFlightModel();
        ProductPricingDetail prd = new ProductPricingDetail();

        [TestMethod]
        public void Testing()
        {
            DiscountDetail testing = new DiscountDetail()
            {
                DiscType = DiscountType.CODE,
                Disc_Amt = 100,
                Disc_Desc = "Promo Code",
                PrdType = ProductTypes.Flight,
                Seq = 1
            };
            prd.DiscountInsert(testing);
            prd.DiscountRemove(testing);
        }

        public PromoCodeRule GetPromoCodeDiscountRule(SearchFlightResultViewModel searchInfo, MayFlower dbCtx = null)
        {
            dbCtx = dbCtx ?? new MayFlower();

            var result = dbCtx.PromoCodeRules
                .Where(
                x => x.IsActive && DateTime.Now >= x.EffectiveFrom && DateTime.Now <= x.EffectiveTo
                && searchInfo.EndDate >= x.TravelDateFrom && searchInfo.BeginDate <= x.TravelDateTo
                && x.PromoCode == searchInfo.PromoCode
                && x.FlightPromo // Suggest use another table to indicate product rather than use column
                );

            //Any Airport = "-"
            Expression<Func<PromoCodeRule, bool>> anyOrigin = (x => x.PromoFlightDestinations.Any(d => d.DepartureStation == "-"
                                                              && d.ArrivalStation == searchInfo.ArrivalStation));
            Expression<Func<PromoCodeRule, bool>> anyDestination = (x => x.PromoFlightDestinations.Any(d => d.ArrivalStation == "-"
                                                                   && d.DepartureStation == searchInfo.DepartureStation));
            Expression<Func<PromoCodeRule, bool>> anyDesAndOri = (x => x.PromoFlightDestinations.Any(d => d.ArrivalStation == "-"
                                                                  && d.DepartureStation == "-"));

            bool anyOriginOK = result.Any(anyOrigin);
            bool anyDestinationOK = result.Any(anyDestination);
            bool anyDesOriOK = result.Any(anyDesAndOri);

            if (anyDesOriOK)
            {
                return result.FirstOrDefault(anyDesAndOri);
            }
            if (anyOriginOK)
            {
                return result.FirstOrDefault(anyOrigin);
            }
            else if (anyDestinationOK)
            {
                return result.FirstOrDefault(anyDestination);
            }
            else
            {
                return result.FirstOrDefault(x => x.PromoFlightDestinations
                       .Any(d => d.DepartureStation == searchInfo.DepartureStation
                                 && d.ArrivalStation == searchInfo.ArrivalStation));
            }
        }
    }
}
