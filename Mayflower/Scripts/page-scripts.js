var co = $('#promo-code');
var coMsg = $('.promo-sMsg');
var cIco = $('.code-checking');
var lBox = $('#loading-modal');
var pBox = $('#popup-modal');
var aCtr = $('#apply-code');

co.keydown(function (e) {
    coMsg.text('');
});

co.on('keyup keydown', function (e) {
    if (e.keyCode === 32) {
        this.value = this.value.replace(' ', '');
    }
});

co.on('blur', function (e) {
    this.value = this.value.toUpperCase().replace(' ', '');
    $.chkCode(this.value);
});

var aCode = function (c, ccn, token) {
    var step = 4;
    var d = { c, token, ccn, step }
    return $.ajax({
        url: '/checkout/applyPCode',
        type: 'POST',
        async: true,
        dataType: 'json',
        data: d,
        beforeSend: function () {
            coMsg.text('');
            cIco.show();
            aCtr.attr('disabled', 'disabled');
            co.attr('disabled', 'disabled');
        },
    }).promise();
}

var rCode = function () {
    return $.ajax({
        url: '/checkout/RemovePCode',
        type: 'POST',
        async: true,
        dataType: 'json',
        beforeSend: function () {
            coMsg.text('');
            lBox.show();
        },
    }).promise();
};

var dCCSwFunc = function (a) {
    if (a) {
        $('.cc-main-container').show();
        $('.cc-disabled-container').hide();
    }
    else {
        $('.cc-main-container').hide();
        $('.cc-disabled-container').show();
    }
}

var encCCN = function (n) {
    if (typeof (n) === 'undefined') {
        return null;
    }

    n = n.replace(new RegExp('0', 'g'), 'Z')
            .replace(new RegExp('1', 'g'), 'h')
            .replace(new RegExp('2', 'g'), 'x')
            .replace(new RegExp('3', 'g'), 'Y')
            .replace(new RegExp('4', 'g'), 'o')
            .replace(new RegExp('5', 'g'), 'R')
            .replace(new RegExp('6', 'g'), 'w')
            .replace(new RegExp('7', 'g'), 'N')
            .replace(new RegExp('8', 'g'), 'c')
            .replace(new RegExp('9', 'g'), 't');

    return n;
}

$.chkCode = function (e) {
    var c = $('#apply-code').siblings().val();
    var ccn = encCCN($('#CreditCardNo').val());
    var m = $('.promo-sMsg');

    if (c.length <= 0) {
        return;
    }
    aCode(c, ccn).then(function (res) {
        if (res.valid) {
            dCCSwFunc(res.awc);
            m.html('');
            cIco.hide();
            $('#pcode_detail > #p_left').html(res.desc);
            $('#pcode_detail > #p_right').html(res.cur + '&nbsp;<span>' + res.amount.toFixed(2) + '</span>');
            $('.eachSection.pcode-summary').show();
            $('.eachSection.pcode-summary > .s2-1_tt_left').html(res.code);
            $('.eachSection.pcode-summary > .s2-1_tt_right').html(res.cur + '&nbsp;<span>' + res.amount.toFixed(2) + '</span>');
            $('.pItem').addClass('hidden');
            if (res.amount2 != null && res.desc2 != null) {
                $('#instantdisc_detail > #p_left').html(res.desc2);
                $('#instantdisc_detail > #p_right').html(res.cur + '&nbsp;<span>' + res.amount2.toFixed(2) + '</span>');
            } else {
                $('#instantdisc_detail > #p_left').html('');
                $('#instantdisc_detail > #p_right').html('');
            }
            $("#p_left:empty").parent().hide();
            $("#p_left:not(:empty)").parent().show();
            if (typeof (res.promptMsg) !== 'undefined' && res.promptMsg != null) {
                coMsg.text(res.promptMsg);
                $('.popup').show();
                $('.modal-container').html("<div class='session_lb_text'>NOTICE</div><div class='session_lb_text1'>"
                    + res.promptMsg +
                    "</div><a href='javascript:;' class='modal-close' style='text-decoration:none'><div class='redbacktohome_button'>Close</div></a>");
            }
            ctrlTCTW(res);
        } else if (res.amount2 != null && res.desc2 != null) {
            $('#instantdisc_detail > #p_left').html(res.desc2);
            $('#instantdisc_detail > #p_right').html(res.cur + '&nbsp;<span>' + res.amount2.toFixed(2) + '</span>');
        }
        else {
            cIco.hide();
            m.html(res.msg);
            // put error msg here
        }
        //alert(res);
    }, function (res) {
        coMsg.text('Unexpected error. Please try again later.');
        pBox.html(res.responseText).show();
        console.log(res);
        }).then(function () {
            aCtr.removeAttr('disabled');
            co.removeAttr('disabled');
        });
}

$('#apply-code').on('click', function (e) {
    $.chkCode(e);
});

$('#pcode_detail').on('click', '#promo-remove', function (e) {
    var m = $('.promo-sMsg');
    rCode().then(function (res) {
        if (typeof(res) === 'undefined') {
            alert('Unexpected error. Please try again later.');
        }
        if (res.valid) {
            dCCSwFunc(true);
            $('#pcode_detail > #p_left').html('');
            $('#pcode_detail > #p_right').html('');
            if (res.amount != null && res.desc != null) {
                $('#instantdisc_detail > #p_left').html(res.desc);
                $('#instantdisc_detail > #p_right').html(res.cur + '&nbsp;<span>' + res.amount.toFixed(2) + '</span>');
            } else {
                $('#instantdisc_detail > #p_left').html('');
                $('#instantdisc_detail > #p_right').html('');
            }
            $("#p_left:empty").parent().hide();
            $("#p_left:not(:empty)").parent().show();
            $('.eachSection.pcode-summary').hide();
            $('.eachSection.pcode-summary > .s2-1_tt_left').html('');
            $('.eachSection.pcode-summary > .s2-1_tt_right').html('');
            $('#apply-code').siblings().val('');
            $('.pItem').removeClass('hidden');
            ctrlTCTW(res);
        }
        else {grdTtl
            m.html(res.msg);
        }
        cIco.hide();
        lBox.hide();
    }, function (res) {
        cIco.hide();
        lBox.hide();
        pBox.html(res.responseText).show();
        coMsg.text('Unexpected error. Please try again later.');
        console.log(res);
        //alert(res);
    });
});

var ctrlTCTW = function (obj, mSel) {
    var _vb = '[name="paymentMethod"]';
    var mSel = $(_vb + ':checked').length > 0 ? $(_vb + ':checked')
        : $($(_vb)[$(_vb).length - 1]);
    var mSelLabel = $("[for=" + $(mSel).attr('id') + "]");
    var m = mSel.val();

    lckMeth();

    var displayTxt = obj.cur + ' ';
    var f = (obj.scUsed || obj.cashCreditUsed) && obj.full;
    var p = (obj.scUsed || obj.cashCreditUsed) && obj.part;

    $('.s4_png_boxright_credit, .s4_png_boxright_price').html('');
    $('.s4_png_boxright_cashcredit').html('');
    dCCSwFunc(obj.awc);
    if (f) {
        if (obj.scUsed) {
            $('.s4_png_boxright_credit').html(displayTxt + obj.scAmt);
        }
        if (obj.cashCreditUsed)
            $('.s4_png_boxright_cashcredit').html(displayTxt + obj.cashCreditAmt);
    }
    else {
        if (p) {

            if (obj.scUsed) {
                $('.s4_png_boxright_credit').html(displayTxt + obj.scAmt);
            }
            if (obj.cashCreditUsed)
                $('.s4_png_boxright_cashcredit').html(displayTxt + obj.cashCreditAmt);
        }

        $(mSelLabel).css('background-position', '0 -37px');
        $("input[name=paymentMethod][value='" + m + "']").prop('checked', true);
        $('.paymentMethod').removeAttr('disabled');
        $('.paymentMethodLabel').removeClass('isInvalid');

        $("[data-pmethod='" + m + "'] .s4_png_boxright_price").html(displayTxt + obj.grdTtl);
    }
    
    if ($('.creditCard-container').length > 0) {
        if (f) {
            $('.creditCard-container').slideUp();
        }
        else if (m.toLowerCase() === 'adyenc') {
            $('.creditCard-container').slideDown();
        }
    }

    if (p || f) {
        $('.TotalPrice').html(obj.nettTtl);
    } else {
        $('.TotalPrice').html(obj.grdTtl);
    }

    $('.Gst').html(obj.ttlGST);
    $('.ProcessingFee').html(obj.ttlPF);
}

var lckMeth = function () {
    $('[name="paymentMethod"]').attr('disabled', 'disabled');
    $('.paymentMethodLabel').addClass('isInvalid');

    $('[name="paymentMethod"]').each(function () {
        var mUnSelLabel = $("[for=" + $(this).attr('id') + "]");
        $(mUnSelLabel).css('background-position', '0 0px');
    });
}
