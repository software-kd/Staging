var updateAmt = function (r) {

    var mSel = $('[name="paymentMethod"]:checked');
    var mSelLabel = $("[for=" + $(mSel).attr('id') + "]");
    var scSel = $('#UseCredit');
    var m = mSel.val();
    var sc = scSel.is(':checked');
    var cc = $("[name = 'EWallet.UseWallet']").is(':checked');

    if (r) {
        lckMeth();
        $(mSelLabel).removeClass('isInvalid');
        $(mSelLabel).css('background-position', '0 -37px');
    }

    calc(m, sc, cc).then(function (obj) {
        if (typeof obj === 'string') {
            $('body').append(obj);
        } else {
            ctrlTCTW(obj);
            $('#loading-modal').hide();
        }
    }, function (xhr, status, error) {
        $('#loading-modal').hide();
        $('#oopsbox').fadeIn();
    });

}

var calc = function (m, c, cc) {
    return $.ajax({
        url: paymentCalcUrl + location.search,
        type: 'POST',
        data: {
            method: m,
            useCredit: c,
            useCashCredit: cc,
        },
        async: true,
        dataType: 'json',
        beforeSend: function () { $('#loading-modal').show() },
    }).promise();
}

/*$(document).ready(function () {
    updateAmt();
});*/

$(".paymentMethod, #UseCredit, [name='EWallet.UseWallet']").on('change', function () {
    var r = $(this).hasClass('paymentMethod');
    updateAmt(r);
});

$('#checkoutForm').on('submit', function (e) {
    var hasContest = $("#contesttnc").length > 0;
    var someNotChecked = !$("#tnc").is(":checked") || !$("#policy").is(":checked") || (hasContest && !$("#contesttnc").is(":checked"));
    var cEle = '#CardholderName, #CreditCardNo, #CVC';
    var hvCard = $(cEle).length != 0;
    var ccInvalid = (hvCard && !$(cEle).valid());

    if (ccInvalid || someNotChecked) {
        e.preventDefault();
        if (someNotChecked) {
            $('.popup').show();
            $('.modal-container').html("<div class='session_lb_text'>IMPORTANT</div><div class='session_lb_text1'>Please accept the " + (hasContest ? "“Buy, Travel & Win” Contest, " : "") + "Terms & Conditions, Protection Act 2010 & Cancellation Policies before proceed.</div><a href='javascript:;' class='modal-close' style='text-decoration:none'><div class='redbacktohome_button'>Close</div></a>");
        }
    }
    else {
        if (typeof GTM_trackAddToCart !== "undefined") {
            sessionStorage.ecomselectedH = JSON.stringify(GTM_trackAddToCart);
        }
        if (typeof GTM_trackAddOnSelected !== "undefined") {
            sessionStorage.ecomselectedA = JSON.stringify(GTM_trackAddOnSelected);
        }
        if (typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}') {
            ecomcheckout(3, $('.paymentMethod:checked').val());
        }
        else {
            if (typeof GTM_trackAddToCart !== "undefined") {
                var products = [];
                GTM_trackAddToCart.forEach(function (room) {
                    products.push(room);
                });
                if (typeof GTM_trackAddOnSelected !== "undefined") {
                    GTM_trackAddOnSelected.forEach(function (addon) {
                        products.push(addon);
                    });
                }
                trackCheckout3(products, $('[name="paymentMethod"]:checked').val());
            }
        }

        if (hvCard) {
            $(this).append('<input type="hidden" name="ccn" value="' + encCCN($('#CreditCardNo').val()) + '"></input>')
        }
    }
})

$('.ars-payment').on('click', function () {
    var nChk = $("#tnc").is(":checked") && $("#policy").is(":checked") && ($("#contesttnc").length == 0 || $("#contesttnc").is(":checked"));
    var notUseAd = $('#CardholderName, #CreditCardNo, #CVC').length == 0;
    if ((nChk && notUseAd) || (nChk && !notUseAd && $('#CardholderName, #CreditCardNo, #CVC').valid())) {
        $('#loading-modal').show();
        $('#oops-msg').html('Loading... Please do not refresh the page.')
        $(this).prop('disabled', true);
    }
});

var checkpaymentradio = function (parentId, creditUsed, priceRemain) {
    var totalPrice = $('.TotalPrice').text();
    var currency = $('.Currency').text();

    if (parentId !== 'travelCredit_div') {
        $('.s4_png_boxright_price').html('');
        //$('#' + parentId).find('.s4_png_boxright_price').html($('#Totalprice').text());
        $('#' + parentId).find('.s4_png_boxright_price').html(currency + ' ' + numberWithCommas(priceRemain));
    }
    else {
        if ($('#checkbox1').is(':checked')) {
            //$('#' + parentId).find('.s4_png_boxright_credit').html($('#Credit').text());
            $('#' + parentId).find('.s4_png_boxright_credit').html(currency + ' ' + numberWithCommas(creditUsed));
        }
        else {
            $('#' + parentId).find('.s4_png_boxright_credit').html('');
        }

        if (priceRemain === "0.00") {
            $('.s4_png_boxright_price').html('');
        } else {
            parentId = $('.paymentMethod:checked').parents(".s4_png_inner_row").prop('id');
            checkpaymentradio(parentId, creditUsed, priceRemain);
        }
    }
}

var closePopOutModal = function () {
    $('#confirmationModal').modal('hide');
}

var numberWithCommas = function (x) {
    var parts = x.toString().split(".");
    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ",");
    return parts.join(".");
}
