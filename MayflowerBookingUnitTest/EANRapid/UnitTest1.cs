using System;
using System.Linq;
using System.Collections.Generic;
using Alphareds.Module.Common;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.EANRapidHotels.RapidServices;
using Alphareds.Module.ESBHotelComparisonWebService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace MayflowerBookingUnitTest.EANRapid
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public SearchRoomModel GetRoomModelWithResult(out SearchHotelModel hotelModel, out string hotelIdPicked)
        {
            hotelModel = InitializeTestingModel.SearchHotelModel("Ipoh, Malaysia", 2, 4, 0, new List<int> { });
            hotelModel.SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
            {
                EANRapid = true,
            };

            hotelModel.ArrivalDate = new DateTime(2018, 12, 10);
            hotelModel.DepartureDate = new DateTime(2018, 12, 15);

            hotelModel.Result = Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelList(hotelModel);

            var hotelPicked = hotelModel.Result?.HotelList?.OrderBy(x => Guid.NewGuid()).FirstOrDefault(x => x.hotelId == "9626874");

            Assert.IsNotNull(hotelPicked);
            hotelIdPicked = hotelPicked.hotelId;

            var roomModel = InitializeTestingModel.SearchRoomHotel(hotelModel, hotelPicked.hotelId);
            roomModel.Result = Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetRoomAvailability(roomModel, hotelModel);

            var roomTokenList = roomModel.Result.HotelRoomInformationList[0].roomAvailabilityDetailsList.SelectMany(s => s.BetTypes).Select(s => s.id);
            var tokenGrpList = roomTokenList.GroupBy(s => s).Select(s => new { c = s.Count(), s.Key }).Where(s => s.c > 2);

            return roomModel;
        }

        [TestMethod]
        public void TestGetRoom()
        {
            GetRoomModelWithResult(out SearchHotelModel hotelModel, out string hotelId);
        }

        [TestMethod]
        public void TestESBBook()
        {
            Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBHotelManagerClient esbHotelClient = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBHotelManagerClient();
            Alphareds.Module.EANRapidHotels.RapidServices.HotelManagerClient rapidHotels = new Alphareds.Module.EANRapidHotels.RapidServices.HotelManagerClient();

            var roomModel = GetRoomModelWithResult(out SearchHotelModel hotelModel, out string hotelId);
            var roomResult = roomModel.Result;

            var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);
            var testPickCount = roomList.Count();
            testPickCount = (int)Math.Floor(testPickCount * 0.3);
            testPickCount = testPickCount <= 0 ? 1 : testPickCount;

            var pickedRoomList = roomList.OrderBy(x => Guid.NewGuid()).Take(testPickCount);

            List<Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBReserveRoomResponse>> bookRoomReqList = new List<Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBReserveRoomResponse>>();
            List<Task<GetCurrentPriceResponse>> getPreBookPrice = new List<Task<GetCurrentPriceResponse>>();

            foreach (var item in roomList)
            {
                var _roomReq = new GetCurrentPriceRequest
                {
                    UserAgent = roomModel.CustomerUserAgent,
                    CustomerIp = roomModel.CustomerIpAddress,
                    Opt_CustomerSessionId = roomModel.CustomerSessionId,
                    PropertyID = Convert.ToInt32(hotelId),
                    RateID = item.RateInfos.FirstOrDefault()?.Rooms.FirstOrDefault()?.rateKey,
                    RoomID = item.roomTypeCode,
                    Token = item.BetTypes.FirstOrDefault()?.id,
                    Opt_Test = Test1.price_changed,
                };

                getPreBookPrice.Add(rapidHotels.GetCurrentPriceForPreBookingAsync(_roomReq));
            }

            while (getPreBookPrice.Count > 0)
            {
                var checkPriceReq = Task.WaitAny(getPreBookPrice.ToArray());
                var _tskCompleted = getPreBookPrice[checkPriceReq];

                if (_tskCompleted.IsFaulted || _tskCompleted.IsCanceled)
                {
                    Assert.IsTrue(_tskCompleted.IsFaulted || _tskCompleted.IsCanceled, "Task Failed.");
                    break;
                }
                else if (_tskCompleted.IsCompleted)
                {
                    var item = _tskCompleted.Result;

                    if (item?.Errors?.ErrorMessage.Length > 0 || item?.links?.book?.token?.Length == 0)
                    {
                        Assert.Fail(item?.Errors?.ErrorMessage ?? "Book link Token is empty.");
                    }

                    // Check Price Matched or not
                    var totalRate = item.occupancies.Sum(x => x.Value.totals.inclusive.billable_currency.value.ToDecimal());
                    // TODO: Check display price is mathced with selected room rate or not.

                    var bookReq = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBReserveRoomRequest
                    {
                        CustomerIpAddress = hotelModel.CustomerIpAddress,
                        CustomerSessionId = hotelModel.CustomerSessionId,
                        CustomerUserAgent = hotelModel.CustomerUserAgent,

                        rateType = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RateType.MerchantStandard,
                        //rateType = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RateType.MerchantStandard,
                        CurrencyCode = "MYR",
                        ArrivalDate = hotelModel.ArrivalDate,
                        DepartureDate = hotelModel.DepartureDate,
                        HotelID = hotelId,
                        HotelSuppliers = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.Suppliers.EANRapid,
                        tokenKey = item.links.book.token,
                        specialInformation = "Test",
                    };

                    bookReq.NumberOfRoom = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.GuestRoomAdditionalInfo[]
                    {
                        new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.GuestRoomAdditionalInfo
                        {
                            Title = "Mr",
                            FirstName = "Test",
                            MiddleName = "Test",
                            LastName = "Test",
                            Email = "test@alphareds.net",
                            HomePhone = "1234567",
                            WorkPhone = "1234567",
                            BedTypeID = "",
                            TotalAdults = roomModel.GuestInRoomDetails.FirstOrDefault()?.Adults ?? 1,
                            NumberOfChildrenAge = new int[]{ },
                            smokingPreference = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SmokingPreference.S
                        },
                        new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.GuestRoomAdditionalInfo
                        {
                            Title = "Mr",
                            FirstName = "Test",
                            MiddleName = "Test",
                            LastName = "Test",
                            Email = "test@alphareds.net",
                            HomePhone = "1234567",
                            WorkPhone = "1234567",
                            BedTypeID = "",
                            TotalAdults = roomModel.GuestInRoomDetails.FirstOrDefault()?.Adults ?? 1,
                            NumberOfChildrenAge = new int[]{ },
                            smokingPreference = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SmokingPreference.S
                        }
                    };

                    bookRoomReqList.Add(esbHotelClient.BookHotelAsync(bookReq));
                }
                else
                {
                    // Throw exception message here, indicate get room availibiity failed.
                    Assert.Fail();
                }

                getPreBookPrice.Remove(_tskCompleted);
            }

            for (int i = 0; i < bookRoomReqList.Count; i++)
            {
                var bookReq = Task.WaitAny(bookRoomReqList.ToArray());
                var _tskCompleted = bookRoomReqList[bookReq];

                if (!_tskCompleted.IsCanceled && !_tskCompleted.IsFaulted && _tskCompleted.IsCompleted)
                {
                    var _result = _tskCompleted.Result;
                    if (_result.Errors?.ErrorMessage?.Length > 0)
                    {
                        break;
                    }
                }

                if (bookRoomReqList.Remove(_tskCompleted))
                {
                    // Minus for loop index.
                    i = i - 1;
                }
            }
        }

        [TestMethod]
        public void TestBookFlow()
        {
            Alphareds.Module.EANRapidHotels.RapidServices.HotelManagerClient rapidHotels = new Alphareds.Module.EANRapidHotels.RapidServices.HotelManagerClient();

            var roomModel = GetRoomModelWithResult(out SearchHotelModel hotelModel, out string hotelId);
            var roomResult = roomModel.Result;

            var bookRequest = new Alphareds.Module.EANRapidHotels.RapidServices.CreateBookingRequest
            {
                UserAgent = roomModel.CustomerUserAgent,
                CustomerIp = roomModel.CustomerIpAddress,
                Opt_Hold = Hold.False,
            };

            bookRequest.Rooms = new List<GuestRoom1>();

            var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);
            var testPickCount = roomList.Count();
            testPickCount = (int)Math.Floor(testPickCount * 0.3);
            testPickCount = testPickCount <= 0 ? 1 : testPickCount;

            var pickedRoomList = roomList.OrderBy(x => Guid.NewGuid()).Take(testPickCount);
            List<Task<GetCurrentPriceResponse>> getPreBookPrice = new List<Task<GetCurrentPriceResponse>>();

            #region Test Rapid Get Room
            var testRoomReq = new GetAvailabilityRequest
            {
                UserAgent = roomModel.CustomerUserAgent,
                CustomerIp = roomModel.CustomerIpAddress,
                Opt_CustomerSessionId = roomModel.CustomerSessionId,

                CheckIn = new DateTime(2018, 12, 10), //hotelModel.ArrivalDate,
                CheckOut = new DateTime(2018, 12, 15), //hotelModel.DepartureDate,
                Currency = hotelModel.CurrencyCode,
                Opt_Token = "REhZAQsABAMGQggMV1pFAV1YVA5cZhBYEgNKH0ZdF105RR9DUAQABlIFTAhAQFxVWhBFBQpWGlwDHFV3AEBVR0dCWF5TQW9GUhFHDFtaPl9cDA1UUFFTUA5WHVIDCgIVBAdQBxoAUAUKGQJZDQ0CUFgFVl0FBx8WUg9WRWsFWwVXWFVfDUJWBkRREgcSElVASghWXz1RBQVSAVdXAwRVABgBV1deGFYGBQAcXlcGVx9SA1dbVgMEVAcMVgMXWVJXVEIDUAELXFZMYWoVRgdBXGZfEkFRXl1eEkdRCFdHOlYLFFsWCgtdBA1MXllbEVQPZ1tWCE4VUwZaA0cEElICZxENWVFeAAVSD0sAVhkIU2FRB0QLJAYFHQRzVFpMBgtcakNRClhGVRAIR1tWQQtHQhhuUFgAAA51PxADWEFQAQkGQltcBwBbWQ0NVwgFWRlUUB5XUUNDU0FMClxKPRRCDl5YDgdnUQdbAgQFVA1cFFIXExQAXwEfXig_MBFaAxBZVUxZC1FqV1tWCg8HWAsAQ1sNUAFbVhdNW1QJCA0ZUgpLAFMVdlEBAF8XTAkJVwUCDF0EXFUBVA==",

                //Opt_FilterResult = new Options
                //{
                //    //Opt_RateOption = RateOption.net_rates,
                //    Opt_Include = Include.all_rates,
                //    Opt_RateOption = RateOption.net_rates,
                //    Opt_Filter = Filter.expedia_collect,
                //},

                Destination = null, //hotelModel.Destination,
                CountryCode = "MY",
                PropertyID = new List<string> { "9626874" },
                SalesEnvironment = SalesEnvironment.hotel_only,
                SalesChannel = SalesChannel.website,
                SortType = SortType.preferred,
                Occupancy = new List<GuestRoom>
                {
                    new GuestRoom
                    {
                        TotalAdults = 2,
                        NumberOfChildrenAge = new List<int>(),
                    }
                    ,new GuestRoom
                    {
                        TotalAdults = 2,
                        NumberOfChildrenAge = new List<int>(),
                    }
                }
            };

            var roomRateRapidTest = rapidHotels.GetPropertyRoomRatesAndAvailability(testRoomReq);
            #endregion

            var test2 = roomRateRapidTest.result[0].rooms.Where(x => x.rates.Any(s => s.bed_groups.Count > 1));

            foreach (var item in pickedRoomList)
            {
                var roomRateRapid = rapidHotels.GetPropertyRoomRatesAndAvailability(new GetAvailabilityRequest
                {
                    UserAgent = roomModel.CustomerUserAgent,
                    CustomerIp = roomModel.CustomerIpAddress,
                    Opt_CustomerSessionId = roomModel.CustomerSessionId,

                    CheckIn = hotelModel.ArrivalDate,
                    CheckOut = hotelModel.DepartureDate,
                    Currency = hotelModel.CurrencyCode,

                    Destination = hotelModel.Destination,
                    CountryCode = "MY",
                    PropertyID = new List<string> { hotelId },
                    SalesEnvironment = SalesEnvironment.hotel_only,
                    SalesChannel = SalesChannel.website,
                    SortType = SortType.preferred,

                    Occupancy = new List<GuestRoom> {
                        new GuestRoom
                        {
                            TotalAdults = 2
                        }
                        ,new GuestRoom
                        {
                            TotalAdults = 2
                        }
                    }
                });

                var _roomReq = new GetCurrentPriceRequest
                {
                    UserAgent = roomModel.CustomerUserAgent,
                    CustomerIp = roomModel.CustomerIpAddress,
                    Opt_CustomerSessionId = roomModel.CustomerSessionId,
                    PropertyID = Convert.ToInt32(hotelId),
                    RateID = item.RateInfos.FirstOrDefault()?.Rooms.FirstOrDefault()?.rateKey,
                    RoomID = item.roomTypeCode,
                    Token = item.BetTypes.FirstOrDefault()?.id,
                };

                getPreBookPrice.Add(rapidHotels.GetCurrentPriceForPreBookingAsync(_roomReq));

                bookRequest.Rooms.Add(new GuestRoom1
                {
                    Email = "test@alphareds.net",
                    Opt_Title = "MR",
                    Family_Name = "TEST",
                    Given_Name = "TEST",
                    Phone = "1234567",
                    Smoking = Smoking.None,
                    Opt_Special_Request = "Test Special Request",
                    Number_Of_Adults = hotelModel.NoOfAdult,
                });
            }

            for (int i = 0; i < getPreBookPrice.Count; i++)
            {
                var bookReq = Task.WaitAny(getPreBookPrice.ToArray());
                var _tskCompleted = getPreBookPrice[bookReq];

                if (!_tskCompleted.IsCanceled && !_tskCompleted.IsFaulted && _tskCompleted.IsCompleted)
                {
                    var _result = _tskCompleted.Result;
                    if (_result.Errors?.ErrorMessage?.Length > 0)
                    {
                        break;
                    }
                }

                if (getPreBookPrice.Remove(_tskCompleted))
                {
                    // Minus for loop index.
                    i = i - 1;
                }
            }

            var res = rapidHotels.CreateBooking(bookRequest);
            Assert.IsTrue(res?.itinerary_id?.Length > 0, "Create Booking Failed.");
        }
    }
}
