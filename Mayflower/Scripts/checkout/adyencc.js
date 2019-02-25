
// The form element to encrypt.
var form = document.getElementById('checkoutForm');
// See https://github.com/Adyen/CSE-JS/blob/master/Options.md for details on the options to use.
var options = {
    "enableValidations": false,
};
// Bind encryption options to the form.
adyen.createEncryptedForm(form, options);
var r = adyen.encrypt.createEncryption();

var url = window.location.href;
if (url.indexOf('fail') > -1) {
    $('.popup').show();
    $('.modal-container').html("<div class='session_lb_text'>IMPORTANT</div><div class='session_lb_text1'>The payment was not successful, please retry by enter payment information again or select another payment method.</div><a href='javascript:;' class='modal-close' style='text-decoration:none'><div class='redbacktohome_button'>Close</div></a>");
    
}

var validateCC = function(n, c, m, y) {
    return r.validate({ "number": n, "cvc": c, "month": m, "year": y });
}

// Check Credit Card //
$(document).ready(function () {
    if ($('#visaMaster:checked').length > 0) {
        $('.creditCard-container').slideDown();
    }
});

$('.paymentMethod').on('change', function () {
    var mSel = $('[name="paymentMethod"]:checked');
    var m = mSel.val();

    if (m == 'ADYENC') {
        $('.creditCard-container').slideDown();
    }
    else {
        $('.creditCard-container').slideUp();
    }
});

$('#CreditCardNo').on('blur', function () {
    var value = $(this).val();
    var value2 = $(this).val().substr(0, 1);
    if (value2 == '4' && value.length == 16) {
        $('#CardType').val("Visa Card");
        $('#CardType').val() == "Visa Card"
    }
    else
        $('#CardType').val("Master Card");
    $('#CardType').val() == "Master Card"
});
