$(function () {
    var co = $('#promo-code');
    var coMsg = $('.promo-sMsg');
    var cIco = $('.code-checking');
    var aCtr = $('#apply-code');

    co.keydown(function (e) {
        coMsg.text('');
    });

    co.on('keyup keydown', function (e) {
        var code = (e.keyCode ? e.keyCode : e.which);
        if (code === 13) {
            this.blur();
        }
        else if (code === 32) {
            this.value = this.value.replace(' ', '');
        }
    });

    co.on('blur', function (e) {
        this.value = this.value.toUpperCase().replace(' ', '');
        $.chkCode(this.value);
    });

    var aCode = function (c, ccn, token) {
        var d = { c, token, ccn }
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
                loadModal(true);
            },
        }).promise();
    };

    var dCCSwFunc = function (a) {
        if (a) {
            $('#UseCredit').removeAttr('disabled');
        }
        else {
            $('#UseCredit').prop('checked', false);
            $('#UseCredit').attr('disabled', 'disabled');
        }
    }

    var dCWSwFunc = function (a) {
        if (a) {
            $('#EWallet\\.UseWallet').removeAttr('disabled');
        }
        else {
            $('#EWallet\\.UseWallet').prop('checked', false);
            $('#EWallet\\.UseWallet').attr('disabled', 'disabled');
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
        var c = $('#apply-code').siblings('input').val();
        var ccn = encCCN($('#CreditCardNo').val());
        var m = $('.promo-sMsg');

        if (c.length <= 0) {
            return;
        }
        aCode(c, ccn).then(function (res) {
            if (res.valid) {
                dCCSwFunc(res.awc);
                dCWSwFunc(res.aww);
                m.html('');
                cIco.hide();
                $('.pcode_detail > #p_left').html(res.desc);
                $('.pcode_detail > #p_right').html(res.cur + '&nbsp;<span>' + res.amount.toFixed(2) + '</span>');
                if (res.amount2 !== null && res.desc2 !== null && typeof (res.amount2) !== 'undefined') {
                    $('#instantdisc_detail > .list-item-desc').html(res.desc2);
                    $('#instantdisc_detail > .list-item-amt').html(res.cur + '&nbsp;<span>' + res.amount2 + '</span>');
                } else {
                    $('#instantdisc_detail > .list-item-desc').html('');
                    $('#instantdisc_detail > .list-item-amt').html('');
                }
                $("#p_left:empty").parent().hide();
                $("#p_left:not(:empty)").parent().show();
                if (typeof (res.promptMsg) !== 'undefined' && res.promptMsg !== null) {
                    dynamicModal('NOTICE', res.promptMsg, false).model();
                    coMsg.text(res.promptMsg);
                }
                ctrlTCTW(res);
                focusPaymentSummary();
            } else if (res.amount2 !== null && res.desc2 !== null && typeof (res.amount2) !== 'undefined') {
                $('#instantdisc_detail > .list-item-desc').html(res.desc2);
                $('#instantdisc_detail > .list-item-amt').html(res.cur + '&nbsp;<span>' + res.amount2 + '</span>');
            }
            else {
                cIco.hide();
                m.html(res.msg);
                // put error msg here
            }
        }, function (res) {
            coMsg.text('Unexpected error. Please try again later.');
            dynamicModal('Error', res.responseText, false).modal();
            console.log(res);
        }).then(function () {
            aCtr.removeAttr('disabled');
            co.removeAttr('disabled');
        });
    }

    var removeCodeEvent = function () {
        var m = $('.promo-sMsg');
        rCode().then(function (res) {
            if (typeof (res) === 'undefined') {
                alert('Unexpected error. Please try again later.');
            }
            if (res.valid) {
                dCCSwFunc(res.awc);
                dCWSwFunc(res.aww);
                $('.pcode_detail > #p_left').html('');
                $('.pcode_detail > #p_right').html('');
                if (res.amount !== null && res.desc !== null && typeof (res.amount2) !== 'undefined') {
                    $('#instantdisc_detail > .list-item-desc').html(res.desc);
                    $('#instantdisc_detail > .list-item-amt').html(res.cur + '&nbsp;<span>' + res.amount + '</span>');
                } else {
                    $('#instantdisc_detail > .list-item-desc').html('');
                    $('#instantdisc_detail > .list-item-amt').html('');
                }

                $('#apply-code').siblings('input').val('');
                $('.pItem').removeClass('hidden');
                ctrlTCTW(res);
                loadModal(false);
            }
            else {
                grdTtl
                m.html(res.msg);
            }
            cIco.hide();
            focusPaymentSummary();
            loadModal(false);
        }, function (res) {
            cIco.hide();
            loadModal(false);
            dynamicModal('Error', res.responseText, false).modal();
            coMsg.text('Unexpected error. Please try again later.');
            console.log(res);
        });
    }

    var updateAmt = function (r) {

        var mSel = $('[name="paymentMethod"]:checked').length > 0 ? $('[name="paymentMethod"]:checked') : $($('[name="paymentMethod"]')[$('[name="paymentMethod"]').length - 1]);
        var scSel = $('#UseCredit');
        var m = mSel.val();
        var sc = scSel.is(':checked');
        var cc = $("[name = 'EWallet.UseWallet']").is(':checked');

        if (r) {
            lckMeth();
        }

        calc(m, sc, cc).then(function (obj) {
            if (typeof obj === 'string') {
                $('body').append(obj);
            } else {
                ctrlTCTW(obj);
                loadModal(false);
            }
        }, function (xhr, status, error) {
            dynamicModal('Error', xhr.responseText, false).modal();
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
            beforeSend: function () {
                loadModal(true);
            },
        }).promise();
    }
    var updateOffAmt = function (r) {

        var mSel = $('[name="paymentMethod"]:checked');
        var m = mSel.val();

        if (r) {
            lckMeth();
        }
        getOffPayment(m).then(function (obj) {
            ctrlAdyen(obj);
            loadModal(false);
        }, function (xhr, status, error) {
            dynamicModal('Error', xhr.responseText, false).modal();
        });
    }

    var getOffPayment = function (m) {
        return $.ajax({
            url: paymentCalcUrl + location.search,
            type: 'POST',
            data: {
                method: m,
            },
            async: true,
            dataType: 'json',
            beforeSend: function () {
                loadModal(true);
            },
        }).promise();
    }

    var ctrlTCTW = function (obj) {
        var _vb = '[name="paymentMethod"]';
        var mSel = $(_vb + ':checked').length > 0 ? $(_vb + ':checked')
            : $($(_vb)[$(_vb).length - 1]);
        var m = mSel.val();

        if (typeof (obj.msg) !== 'undefined' && obj.msg !== '' && obj.msg !== null) {
            dynamicModal('Error', obj.msg, false).modal();
        }

        lckMeth();

        var displayTxt = obj.cur + ' ';
        var f = (obj.scUsed || obj.cashCreditUsed) && obj.full;
        var p = (obj.scUsed || obj.cashCreditUsed) && obj.part;
        var tcEle = $('.payment-methods [data-pmethod="TC"]');
        var twEle = $('.payment-methods [data-pmethod="TW"]')

        $('.payment-methods p span, .payment-methods p i').html('');
        $('.payment-single').removeClass('in');

        dCCSwFunc(obj.awc);
        dCWSwFunc(obj.aww);

        if (obj.scUsed) {
            tcEle.addClass('in');
            tcEle.find('p i').html(displayTxt + obj.scAmt);
        }
        if (obj.cashCreditUsed) {
            twEle.addClass('in');
            twEle.find('p i').html(displayTxt + obj.cashCreditAmt);
        }

        if (f) {
            //$("input[name=paymentMethod][value='" + m + "']").prop('checked', false);
            /* Full Credit Handle Code Area */
        }
        else {
            $("input[name=paymentMethod][value='" + m + "']").prop('checked', true);
            $('.paymentMethod').removeAttr('disabled');
            $("[data-pmethod='" + m + "']").addClass('in');
            $("[data-pmethod='" + m + "'] p span").html(displayTxt + obj.grdTtl);
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
            $('.reservation-total').html(displayTxt + obj.nettTtl);
            if (obj.nettTtlpax !== null) {
                $('.reservation-ttlperpax').html(displayTxt + obj.nettTtlpax);
            }
        } else {
            $('.TotalPrice').html(obj.grdTtl);
            $('.reservation-total').html(displayTxt + obj.grdTtl);
            if (obj.grdTtlpax !== null) {
                $('.reservation-ttlperpax').html(displayTxt + obj.grdTtlpax);
            }
        }
        if (obj.ttldeposit !== null && typeof obj.ttldeposit !== "undefined") {
            $('.DepositPrice').html(obj.ttldeposit);
            $("[data-pmethod='" + m + "'] p span").html(displayTxt + obj.depositAfterDisc);
        }

        $('.Gst').html(obj.ttlGST);
        $('.ProcessingFee').html(obj.ttlPF);
    }

    var ctrlAdyen = function (obj) {
        var _vb = '[name="paymentMethod"]';
        var mSel = $(_vb + ':checked').length > 0 ? $(_vb + ':checked')
            : $($(_vb)[$(_vb).length - 1]);
        var m = mSel.val();

        lckMeth();

        var displayTxt = obj.cur + ' ';

        $('.payment-methods p span, .payment-methods p i').html('');
        $('.payment-single').removeClass('in');
        $("input[name=paymentMethod][value='" + m + "']").prop('checked', true);
        $('.paymentMethod').removeAttr('disabled');
        $("[data-pmethod='" + m + "']").addClass('in');
        $("[data-pmethod='" + m + "'] p span").html(displayTxt + obj.grdTtl);
        $('.TotalPrice').html(obj.grdTtl);
        $('.ProcessingFee').html(obj.pfee);
        $('.reservation-total').html(displayTxt + obj.grdTtl);
    }

    var lckMeth = function () {
        $('[name="paymentMethod"]').attr('disabled', 'disabled');
    }

    var numberWithCommas = function (x) {
        var parts = x.toString().split(".");
        parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ",");
        return parts.join(".");
    }

    var closePopOutModal = function () {
        $('#confirmationModal').modal('hide');
    }

    var testDateExpire = function (str) {
        var patt = new RegExp("^((([0][1-9])|[1][0-2])\/((2[0][1][8-9])|(2[0-9][2-9][0-9])))$");
        return patt.test(str);
    }

    var focusPaymentSummary = function () {
        $(window).scrollTop($('.payment-sum').position().top);
    }

    $('#apply-code').on('click', function (e) {
        $.chkCode(e);
    });

    $('.pcode_detail').on('click', '#promo-remove', function (e) {
        removeCodeEvent();
    });

    $('#CVV').on('keyup keydown', function (e) {
        var val = $(this).val();

        if (val.length === 0) {
            $('#ExpMonths, #ExpYear').val('0');
        }

        if (e.keyCode !== 8) {
            //var monthTest = new RegExp("^[0-1][0-9]$");
            if (val.length === 2) {
                $('#ExpMonths').val(val);
                $(this).val(val + '/');
            }
            else if (val.length === 7) {
                $('#ExpYear').val(val.substring(3, val.length));
            }
        }
        else if (e.keyCode === 8) {
            if (val.length === 3) {
                $(this).val(val.replace('/', ''));
            }
        }
    });

    //Toggle payment card form
    $('.payment-top label').on('click', function () {
        var el = $(this).find('input[type="radio"]')
        if (el.length > 0 && !el.prop('checked') && !el.prop('disabled')) {
            $('.payment-single').removeClass('in');
            $(this).parent('.payment-top').parent('.payment-single').addClass('in');
        }
    });

    $(".paymentMethod, #UseCredit, [name='EWallet.UseWallet']").on('change', function () {
        var r = $(this).hasClass('paymentMethod');
        var o = $(".payment-methods").hasClass('offlinePay');
        if (o) {
            updateOffAmt(r);
        } else {
            updateAmt(r);
        }
    });

    $(document).on('submit', '#checkoutForm', function (e) {
        if ($('#processingModal').length > 0 && $('#processingModal').is(':visible')) {
            e.preventDefault();
        }

        var cEle = '#CardholderName, #CreditCardNo, #CVC';
        var hvCard = $(cEle).length !== 0;
        var ccInvalid = (hvCard && !$(cEle).valid());

        if (hvCard && $('#CreditCardNo').is(':visible')) {
            try {
                var res = validateCC($('#CreditCardNo').val(), $('#CVC').val(), $('#ExpMonths').val(), $('#ExpYear').val());

                if (!res.valid) {
                    e.preventDefault();
                    dynamicModal('ERROR', "Unacceptable card detected.", false).modal();
                }
            } catch (e) {
                console.log(e);
            }
        }

        var hasContest = $("#contesttnc").length > 0;
        var agEle = $('#b2bagree');
        var isAg = agEle.length > 0;
        var someNotChecked = !$("#tnc").is(":checked") || !$("#policy").is(":checked") || (hasContest && !$("#contesttnc").is(":checked"))
            || (isAg && !agEle.is(":checked"));

        if (ccInvalid || someNotChecked) {
            e.preventDefault();
            if (someNotChecked) {
                $(window).scrollTop($('.payment-terms').position().top);
                dynamicModal('IMPORTANT', "Please accept the "
                    + (hasContest ? "“Buy, Travel & Win” Contest, " : "")
                    + (isAg ? "B2B Package Rate Acknowledges, " : "")
                    + "Terms & Conditions, Protection Act 2010 & Cancellation Policies before proceed."
                    , false).modal();
            }
        }
        else {
            if ($('#processingModal').length > 0) {
                $('#processingModal').modal({ backdrop: 'static', keyboard: false, show: true });
            }

            if ($(".offlinePay").length === 0) {
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
            }
            if (hvCard) {
                $(this).append('<input type="hidden" name="ccn" value="' + encCCN($('#CreditCardNo').val()) + '"></input>');
            }
        }
    });

    $('.ars-payment').on('click', function () {
        var nChk = $("#tnc").is(":checked") && $("#policy").is(":checked") &&
                    ($("#contesttnc").length === 0 || $("#contesttnc").is(":checked")) &&
                    ($("#b2bagree").length === 0 || $("#b2bagree").is(":checked"));
        var notUseAd = $('#CardholderName, #CreditCardNo, #CVC').length === 0;
        if ((nChk && notUseAd) || (nChk && !notUseAd && $('#CardholderName, #CreditCardNo, #CVC').valid())) {
            loadModal(true);
            $(this).prop('disabled', true);
        }
    });

    $('#btnPageSubmit').on('click', function () {
        //$('#checkoutForm').submit();
        $("button#submitpayment").trigger('click');
        //$("button.d-none[type=submit]").trigger('click');
    });
});