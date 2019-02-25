var dataLayer = window.dataLayer = window.dataLayer || [];

(function () {
    var initgadata = function () {
        var gaPType = [];
        var gaPTrip = searchCriteria.TripType === 'Return' ? 'Round Trip' : (searchCriteria.TripType === 'OneWay' ? 'One Way' : 'undefined');
        if (searchCriteria.Adults > 0) {
            gaPType.push(searchCriteria.Adults + ' Adults');
        }
        if (searchCriteria.Childrens > 0) {
            gaPType.push(searchCriteria.Childrens + ' Children');
        }
        if (searchCriteria.Infants > 0) {
            gaPType.push(searchCriteria.Infants + ' Infants');
        }

        var departDate = moment(searchCriteria.BeginDate, "YYYY-MM-DD").format('DD-MMM-YYYY');
        var returnDate = moment(searchCriteria.EndDate, "YYYY-MM-DD").format('DD-MMM-YYYY');

        dataLayer.push({
            'userID': typeof loggeduser === "undefined" ? '' : loggeduser, // layout
            'name': searchCriteria.DepartureStationCode + '_' + searchCriteria.ArrivalStationCode,
            'travelFrom': searchCriteria.DepartureStation,
            'travelTo': searchCriteria.ArrivalStation,
            'fromDate': departDate,
            'toDate': returnDate === 'Invalid date' ? '-' : returnDate,
            'preferredAirline': searchCriteria.PrefferedAirlineCode,
            'tripRounds': gaPTrip,
            'numberOfPassengers': gaPType.join(),
            'cabinClass': searchCriteria.CabinClassName
        });
    }

    if (typeof searchCriteria !== "undefined") {
        initgadata();
    }
})(window, document);

(function () {
    var initgadata = function () {
        var data = GTM_trackHotelSearchCriteria;
        var arrData = data.split('|');
        var dataLayer = window.dataLayer = window.dataLayer || [];
        dataLayer.push({
            'userID': arrData[0] === '0' ? '' : arrData[0],  // User ID for logged-in customers
            'name': arrData[1],  // city name
            'fromDate': arrData[2],
            'toDate': arrData[3],
            'numberOfNights': arrData[4],
            'numberOfPassengers': arrData[5],
            //'numberOfRooms': arrData[6],
            'staying': arrData[7]
        });
    }

    if (typeof GTM_trackHotelSearchCriteria !== "undefined") {
        initgadata();
    }
})(window, document);

(function (w, d, s, l, i) {
    if (!initgafn) {
        //sessionStorage.ecomselected = '{}';
        return false;
    }

    w[l] = w[l] || []; w[l].push({
        'gtm.start':
        new Date().getTime(), event: 'gtm.js'
    }); var f = d.getElementsByTagName(s)[0],
    j = d.createElement(s), dl = l != 'dataLayer' ? '&l=' + l : ''; j.async = true; j.src =
    'https://www.googletagmanager.com/gtm.js?id=' + i + dl; f.parentNode.insertBefore(j, f);
})(window, document, 'script', 'dataLayer', 'GTM-TGC3SZ8');