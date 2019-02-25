var calc = function (m, c) {
    return $.ajax({
        url: '/Hotel/PaymentCalc' + location.search,
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

//var getStoreCredit = function (checkbox) {
//    var action = 'add';
//    if ($(checkbox).is(':checked')) {
//        $('#CreditAmountModels_UseCredit').val('True');
//        action = 'deduct';
//    }
//    else {
//        $('#CreditAmountModels_UseCredit').val('False');
//        action = 'add';
//    }
    
//    var dfd = $.Deferred();

//    totalPriceUsingCredit(action).then(function (obj) {

//        credit = obj.CreditUsed.toFixed(2);
//        price = obj.TotalAmount.toFixed(2);
//        var useCredit = obj.UseCredit;
//        var totalPrice = obj.TotalAmount;
//        fullCreditPayment = obj.FullCreditPayment;

//        if (fullCreditPayment && useCredit) {
//            $('.paymentMethod').prop('checked', false);
//            $('.paymentMethod').attr('disabled', 'disabled');
//            $('.paymentMethodLabel').addClass('isInvalid');
//            $('#Payment_PaymentMethodCode').val('SC');
//        }

//        if (action == 'add') {
//            $("input[name=paymentMethod][value='" + paymentSelected + "']").prop('checked', true);
//            $('#Payment_PaymentMethodCode').val(paymentSelected);
//            $('.paymentMethod').removeAttr('disabled');
//            $('.paymentMethodLabel').removeClass('isInvalid');
//        }
//    }, function (jqXHR, textStatus, error) {
//        $('#loading-modal').hide();
//        $('#oopsbox').fadeIn();
//    }).then(function () {
//        getProcessingFee($('#Payment_PaymentMethodCode').val()).done(function () {
//            dfd.resolve();
//        });
//    });

//    return dfd;
//}

//var totalPriceUsingCredit = function (action) {
//    return $.ajax({
//        url: '/Hotel/deductTotalPriceUsingCredit' + location.search,
//        type: 'POST',
//        data: { action: action },
//        async: true,
//        dataType: 'json',
//    }).promise();
//}

//var getProcessingFee = function (paymentMethod) {
//    var params = { paymentMethod: paymentMethod };
//    return $.ajax({
//        beforeSend: function () {
//            $('#loading-modal').show();
//        },
//        url: '/Hotel/getProcessingFee' + location.search + '&' + $.param(params),
//        type: 'POST',
//        async: true,
//        contentType: "json",
//    }).then(function (obj) {
//        if (typeof obj == 'string') {
//            $('body').append(obj);
//        } else {
//            credit = obj.CreditAmountModels.CreditUsed.toFixed(2);
//            var sourcePrice = obj.TotalAmountWithProcessingFee.toFixed(2);
//            if (credit >= 0 && !fullCreditPayment) {
//                price = (sourcePrice - credit).toFixed(2);
//            } else if (fullCreditPayment) {
//                price = 0;
//            }

//            $('.Gst').html(numberWithCommas(obj.Gst.toFixed(2)));
//            $('.ProcessingFee').html(numberWithCommas(obj.ProcessingFeeModel.ProcessingFee.toFixed(2)));
//            $('.TotalPrice').html(numberWithCommas(sourcePrice));

//            if (paymentMethod != 'SC') {
//                paymentSelected = paymentMethod;
//            }

//            $('#loading-modal').hide();
//        };
//    }, function (jqXHR, textStatus, error) {
//        $('#loading-modal').hide();
//        $('#oopsbox').fadeIn();
//    }).promise();
//}