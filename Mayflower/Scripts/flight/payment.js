var calc = function (m, c) {
    return $.ajax({
        url: '/Booking/PaymentCalc' + location.search,
        type: 'POST',
        data: {
            method: m,
            useCredit: c
        },
        async: true,
        dataType: 'json',
        beforeSend: function () { $('#loading-modal').show() },
    }).promise();
}