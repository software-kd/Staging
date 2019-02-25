var ecomimpressions = function () {
    //var a = JSON.stringify(gaProductlist());
    dataLayer.push({
        'event': 'ecom-impressions',
        'ecommerce': {
            'currencyCode': 'MYR', // Local currency that is shown
            'products': gaProductlist(),
        }
    });
}

var ecomclick = function (position, stop, flightDirection) {
    var cache = gaProductlist()[position];
    cache.numberOfStops = stop;
    cache.direction = flightDirection;
    dataLayer.push({
        'event': 'ecom-click',
        'products': [cache]
    });
}

var ecomdetail = function (position, stop, flightDirection) {
    var cache = gaProductlist()[position];
    cache.numberOfStops = stop;
    cache.direction = flightDirection;
    delete cache['position'];
    var a = JSON.stringify(cache);
    dataLayer.push({
        'event': 'ecom-detail',
        'products': [cache]
    });
}

var ecomselect = function (position, stop) {
    var cache = gaProductlist()[position];
    cache.numberOfStops = stop;
    cache.quantity = searchCriteria.Adults + searchCriteria.Childrens + searchCriteria.Infants;
    delete cache['position'];
    sessionStorage.ecomselected = JSON.stringify(cache);
    dataLayer.push({
        'event': 'ecom-select',
        'products': [cache]
    });
}

var ecomremove = function () {
    var valid2 = typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}';
    if (valid2) {
        var a = JSON.parse(sessionStorage.ecomselected);
        dataLayer.push({
            'event': 'ecom-remove',
            'products': [a]
        });
        sessionStorage.removeItem('ecomselected');
    }
}

var ecomcheckout = function (step, paymentMethod) {
    var a = JSON.parse(sessionStorage.ecomselected);
    var products = [];
    products.push(a);
    if (step == 2 && !a.isDynamic) {
        sessionStorage.removeItem('ecomselectedH');
    }
    if (typeof sessionStorage.ecomselectedH !== 'undefined' && sessionStorage.ecomselectedH !== '' && sessionStorage.ecomselectedH !== '{}') {
        var h = JSON.parse(sessionStorage.ecomselectedH);
        h.forEach(function (room) {
            products.push(room);
        });
    }
    if (step == 3) {
        if (typeof sessionStorage.ecomselectedA !== 'undefined' && sessionStorage.ecomselectedA !== '' && sessionStorage.ecomselectedA !== '{}') {
            var x = JSON.parse(sessionStorage.ecomselectedA);
            x.forEach(function (addon) {
                products.push(addon);
            });
        }
    } else {
        sessionStorage.removeItem('ecomselectedA');
    }
    var o = step === 1 ? 'Contact Details' : (step === 2 ? 'Guest Information' : (step === 3 ? paymentMethod : 'undefined'));
    dataLayer.push({
        'event': 'ecom-checkout',
        'step': step,
        'option': o,
        'ecommerce': {
            'products': products,
        }
    });
}

// 2017/01/09 - uncompleted section
var ecompurchase = function (i, a, t, c) {
    var valid = typeof i !== 'undefined' && typeof a !== 'undefined' && typeof t !== 'undefined';
    var valid2 = typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}';
    if (valid && valid2) {
        var products = [];
        var p = JSON.parse(sessionStorage.ecomselected);
        products.push(p);
        if (typeof sessionStorage.ecomselectedH !== 'undefined' && sessionStorage.ecomselectedH !== '' && sessionStorage.ecomselectedH !== '{}') {
            var GTM_hotel = JSON.parse(sessionStorage.ecomselectedH);
            GTM_hotel.forEach(function (room) {
                products.push(room);
            });
        }
        if (typeof sessionStorage.ecomselectedA !== 'undefined' && sessionStorage.ecomselectedA !== '' && sessionStorage.ecomselectedA !== '{}') {
            var GTM_AddOn = JSON.parse(sessionStorage.ecomselectedA);
            GTM_AddOn.forEach(function (addon) {
                products.push(addon);
            });
        }
        dataLayer.push({
            'event': 'ecom-purchase',
            'transaction': {
                'id': i, // Transaction ID. Required  
                'revenue': a, // Total transaction value (including any tax)
                'tax': t,
                'coupon': typeof c === 'undefined' ? null : c, // ex.'Sale Dec 2016', Coupon/Promotion Code - If no coupon, set to null
            },
            'products': [products]
        });
        sessionStorage.removeItem('ecomselected');
        sessionStorage.removeItem('ecomselectedH');
        sessionStorage.removeItem('ecomselectedA');
    }
}

var gaProductlist = function (activeFilters) {
    if (typeof activeFilter !== "undefined" && activeFilter === "Airlines") {
        activeFilter = "All Flights List" || "Operator List" // Operator List, Times, Stops, Price
    }
    var list = [];
    var gaPType = [];
    var gaPName = searchCriteria.DepartureStationCode + '_' + searchCriteria.ArrivalStationCode;
    var gaPTrip = searchCriteria.TripType === 'Return' ? 'Round Trip' : (searchCriteria.TripType === 'OneWay' ? 'One Way' : 'Undefined');
    var gaPlist = 'All Flights List' + ' - ' + $('#sort-option option:selected').text();
    var gaPCabin = $('.cabin-name').text();
    var currentPage = parseInt($('.pagination').find('.active > a').text());
    var prevCount = isNaN(currentPage) ? 0 : (currentPage - 1) * 5;
    var isDynamic = $("#SearchFlightResultViewModel_IsDynamic").val() == "True";
    if (searchCriteria.Adults > 0) {
        gaPType.push('Adults');
    }
    if (searchCriteria.Childrens > 0) {
        gaPType.push('Children');
    }
    if (searchCriteria.Infants > 0) {
        gaPType.push('Infants');
    }

    $('.flight_group_container').each(function (index, element) {

        var product = {
            name: gaPName,
            id: '',
            price: $(element).find('.price_perppl').text().replace(',', ''),
            operator: $(element).find('.operator').text(),
            tripRounds: gaPTrip,
            passengerType: gaPType.join(),
            cabinClass: gaPCabin,
            list: gaPlist,
            position: (index + 1) + prevCount,
            isDynamic: isDynamic
        };
        list.push(product);
    });
    return list;
}

/*START: hotel GA & GTM*/
//(1) Tracking Hotel Booking Information
var trackHotelSearchCriteria = function (data) {
    try {
        var arrData = data.split('|');
        var dataLayer = window.dataLayer = window.dataLayer || [];
        dataLayer.push({
            'userID': arrData[0],  // User ID for logged-in customers
            'name': arrData[1],  // city name
            'fromDate': arrData[2],
            'toDate': arrData[3],
            'numberOfNights': arrData[4],
            'numberOfPassengers': arrData[5],
            //'numberOfRooms': arrData[6],
            'staying': arrData[7]
        });

        //'userID': 'abc-123465789',  // User ID for logged-in customers
        //'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
        //'fromDate': '23-Feb-2017', 
        //'toDate': '02-Mar-2017', 
        //'numberOfNights': '7',
        //'numberOfPassengers': '2 Adult,1 Child',
        //'numberOfRooms': '1', 
        //'staying': 'All Stars'
    } catch (e) {
        //alert('step1' + e.message);
    }
}

//(2) Tracking Search Results Impressions
var trackHotelSearchResults = function (hotelList, currencyCode) {
    var currency = typeof currencyCode !== 'undefined' ? currencyCode : 'MYR';
    var products = JSON.parse(hotelList); // need to push as object, instead of string
    var dataLayer = window.dataLayer = window.dataLayer || [];
    dataLayer.push({
        'event': 'ecom-impressions',
        'ecommerce': {
            'currencyCode': currency,  // Local currency that is shown
            'products': products   // List all flights that are loaded into view
        }        
    });
    /*
    [   // List all flights that are loaded into view
        {
            'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
            'id': '123456',      // Hotel ID if present
            'price': '323.80',  // Price shown
            'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
            'roomType': 'Standard Room - Advance Saver Rate',
            'list': 'All Hotels - Best Deal', // activeFilters + " - " + selectedOptionFromSortMenu
            'position': 1 // Hotel position on search results page
        }
    ]
    */
}

//(3) Tracking Hotel Detail Views on Search Results
var trackHotelDetail = function (Hotel, eve) {
    var dataLayer = window.dataLayer = window.dataLayer || [];
    var Products = JSON.parse(Hotel);
    var events = "";
    switch (eve) {
        case "select": events = "ecom-select"; break;
        case "detail": events = "ecom-detail"; break;
        default: events = "ecom-detail"; break;
    }

    dataLayer.push({
        'event': events,
        'products': Products
    });
    //'products': {
    //    'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
    //    'id': '123456',      // Hotel ID if present
    //    'price': '323.80',   // Price shown
    //    'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
    //    'roomType': 'Standard Room - Advance Saver Rate',
    //    'list': 'All Hotels - Best Deal', // activeFilters + " - " + selectedOptionFromSortMenu
    //    'position': 1 // Hotel position on search results page
    //}
}

//(4) Tracking Add to Cart Actions
var trackAddToCart = function (selectedRoom) {
    var dataLayer = window.dataLayer = window.dataLayer || [];
    dataLayer.push({
        'event': 'ecom-select',
        'currencyCode': 'MYR',
        'products': selectedRoom
    });

    //'products': {
    //    'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
    //    'id': '123456',      // Hotel ID if present
    //    'price': '323.80',   // Price shown
    //    'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
    //    'roomType': 'Standard Room - Advance Saver Rate'
    //}
}

//(5) Tracking Remove from Cart Actions
var trackRemoveCart = function(selectedRoom){
    var dataLayer = window.dataLayer = window.dataLayer || [];
    dataLayer.push({
        'event': 'ecom-remove',
        'products': {
            'name': 'London, England, United Kingdom',  // city name
            'id': '123456',      // Hotel ID if present
            'price': '323.80',   // Price shown
            'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
            'roomType': 'Standard Room - Advance Saver Rate'
        }
    });

    //'products': {
    //    'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
    //    'id': '123456',      // Hotel ID if present
    //    'price': '323.80',   // Price shown
    //    'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
    //    'roomType': 'Standard Room - Advance Saver Rate'
    //}
}

//(6) Tracking Checkout Step 1) Contact Details
var trackCheckout1 = function (selectedRoom) {
    var products = [];
    var isPackage = selectedRoom[0].isDynamic;
    if (isPackage && typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}') {
        products.push(JSON.parse(sessionStorage.ecomselected));
    } else {
        sessionStorage.removeItem('ecomselected');
    }
    selectedRoom.forEach(function (room) {
        products.push(room);
    });
    sessionStorage.ecomselectedH = JSON.stringify(selectedRoom);
    var dataLayer = window.dataLayer = window.dataLayer || [];
    dataLayer.push({
        'event': 'ecom-checkout',
        'step': 1,
        'option': 'Contact Details',	// Name of the form
        'ecommerce': {
            'products': products,
        }
    });

    //'products': {
    //    'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
    //    'id': '123456',      // Hotel ID if present
    //    'price': '323.80',   // Price shown
    //    'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
    //    'roomType': 'Standard Room - Advance Saver Rate'
    //}
}

//(7) Tracking Checkout Step 2) Guests Information is submitted
var trackCheckout2 = function (selectedRoom) {
    var products = [];
    var isPackage = selectedRoom[0].isDynamic;
    if (isPackage && typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}') {
        products.push(JSON.parse(sessionStorage.ecomselected));
    } else {
        sessionStorage.removeItem('ecomselected');
    }
    selectedRoom.forEach(function (room) {
        products.push(room);
    });
    var dataLayer = window.dataLayer = window.dataLayer || [];
    dataLayer.push({
        'event': 'ecom-checkout',
        'step': 2,
        'option': 'Guest Information', // Name of the form
        'ecommerce': {
            'products': products,
        }
    });
    //'products': {
    //    'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
    //    'id': '123456',      // Hotel ID if present
    //    'price': '323.80',   // Price shown
    //    'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
    //    'roomType': 'Standard Room - Advance Saver Rate'
    //}
}
var trackSelectAddOn = function (selectedAddon) {
    var dataLayer = window.dataLayer = window.dataLayer || [];
    dataLayer.push({
        'event': 'ecom-select',
        'currencyCode': 'MYR',
        'products':selectedAddon
    });
}

//(8) Tracking Checkout Step 3) Clicks on Pay Now 
var trackCheckout3 = function (selectedRoom, paymentMethod) {
    var method = typeof paymentMethod === "undefined" ? 'FPX' : paymentMethod;
    var products = [];
    var isPackage = selectedRoom[0].isDynamic;
    if (isPackage && typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}') {
        products.push(JSON.parse(sessionStorage.ecomselected));
    } else {
        sessionStorage.removeItem('ecomselected');
    }
    selectedRoom.forEach(function (room) {
        products.push(room);
    });
    var dataLayer = window.dataLayer = window.dataLayer || [];
    dataLayer.push({
        'event': 'ecom-checkout',
        'step': 3,
        'option': method, // Selected Payment Method: Credit Card/Debit Card or FPX
        'ecommerce': {
            'products': products,
        }
    });
    //'products': {
    //    'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
    //    'id': '123456',      // Hotel ID if present
    //    'price': '323.80',   // Price shown
    //    'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
    //    'roomType': 'Standard Room - Advance Saver Rate'
    //}
}

//(9) Tracking Purchases (Transactions)
var trackTransaction = function (i, a, t, c) {
    var valid = typeof i !== 'undefined' && typeof a !== 'undefined' && typeof t !== 'undefined';
    var valid2 = typeof sessionStorage.ecomselectedH !== 'undefined' && sessionStorage.ecomselectedH !== '' && sessionStorage.ecomselectedH !== '{}';
    if (valid && valid2) {
        var products = [];
        var p = JSON.parse(sessionStorage.ecomselectedH);
        p.forEach(function (room) {
                products.push(room);
        });
        if (typeof sessionStorage.ecomselectedA !== 'undefined' && sessionStorage.ecomselectedA !== '' && sessionStorage.ecomselectedA !== '{}') {
            var GTM_AddOn = JSON.parse(sessionStorage.ecomselectedA);
            GTM_AddOn.forEach(function (addon) {
                products.push(addon);
            });
        }
        var dataLayer = window.dataLayer = window.dataLayer || [];
        dataLayer.push({
            'event': 'ecom-purchase',
            'transaction': {
                'id': i, // Transaction ID. or unique Booking Number Required  
                'revenue': a, // Total transaction value (including any tax)
                'tax': t,
                'coupon': c // Coupon/Promotion Code - If no coupon, set to null
            },
            'products': p
        });
        sessionStorage.removeItem('ecomselectedH');
        sessionStorage.removeItem('ecomselectedA');
    }
    //'products': {
    //    'name': 'Hotel - London, England, United Kingdom',  // “Hotel - ” + city name
    //    'id': '123456',      // Hotel ID if present
    //    'price': '323.80',   // Price shown
    //    'hotelName': 'Holiday Inn Express London - Stratford', // Name of the hotel as it is shown
    //    'roomType': 'Standard Room - Advance Saver Rate'
    //}
}
/*END: hotel GA & GTM*/