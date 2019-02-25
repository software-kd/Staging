window.addEventListener("scroll", stTop);

function stTop() {
    var b = document.querySelector('.stick-top');
    if (b != null) {
        var h = b.offsetHeight;
        if (document.body.scrollTop > h || document.documentElement.scrollTop > h) {
            b.style.position = 'fixed';
        } else {
            b.style.position = 'static';
        }
    }
}


$("div").on('click', '.modal-close', function () {
    reloadScrollBars();
    $(".fullcover_div5").fadeOut();
    $("#modal-container").empty();
});

function run_waitMe(className, effect) {
    $('.' + className).waitMe({
        effect: effect,
        text: 'Please wait...',
        bg: 'rgba(255,255,255,0.7)',
        color: '#000',
        sizeW: '',
        sizeH: '',
        source: '',
        onClose: function () { }
    });
}

function close_waitMe(className, effect) {
    $('.' + className).waitMe('hide');
}

function submit_form(selector, param, message) {
    param = typeof param != "undefined" ? "?" + param : "";
    var url = $(selector).data('url') + param;

    if (typeof message == "undefined") {
        $('<form action="' + url + '" method="POST"/>')
            .appendTo($(document.body))
            .submit();
    }
    else {
        if (confirm(message)) {
            $('<form action="' + url + '" method="POST"/>')
                .appendTo($(document.body))
                .submit();
        }
    }
}

function back() {
    window.history.back();
}

$(document).on('click', '.back-sp', function () {
    var location = $(this).data('location');
    if (typeof location == 'undefined') {
        if (document.referrer == "") {
            window.history.go(-1);
        }
        else {
            window.location.replace(document.referrer);
        }
    }
    else {
        window.location.replace(location);
    }
});

function getServerTime() {
    var time = null;
    $.ajax({
        url: '/Public/GetCurrentDate',
        async: false, dataType: 'text',
        success: function (text) {
            time = new Date(text);
        }, error: function (http, message, exc) {
            time = new Date();
        }
    });
    return time;
}

var airImg = function (ffcode) {
    if (typeof ffcode != "undefined") {
        return $.ajax({
            url: '/Public/GetAirlineImage',
            type: "GET", dataType: "json",
            data: { airlinecode: ffcode },
        }).promise();
    }
}

var calAge = function (dateInput) {
    var today = ServerDateTime;
    var age = moment(today).diff(moment(new Date(dateInput)), 'years');
    return age;
}

var smoothScroll = function (element) {
    $('html, body').animate({
        scrollTop: $(element).offset().top
    }, 300);
}

var initSlider = function (element) {
    $(element).responsiveSlides({
        auto: true,
        nav: true,
        speed: 500,
        namespace: "callbacks",
        pager: true,
    });
}

function getModelName(fieldName) {
    return fieldName.substr(fieldName.lastIndexOf(".") + 1, fieldName.length);
}

function getModelPrefix(fieldName) {
    return fieldName.substr(0, fieldName.lastIndexOf(".") + 1);
}

function appendModelPrefix(value, prefix) {
    if (value.indexOf("*.") === 0) {
        value = value.replace("*.", prefix);
    }
    return value;
}

Array.prototype.isUnique = function () {
    var uniq = [];
    var result = this.slice(0).every(function (item, index, arr) {
        if (uniq.indexOf(item) > -1) {
            arr.length = 0;
            return false;
        } else {
            uniq.push(item);
            return true;
        }
    });
    return result;
};

function reloadScrollBars() {
    document.documentElement.style.overflow = 'auto';  // firefox, chrome
    document.body.scroll = "yes"; // ie only
}

function unloadScrollBars() {
    document.documentElement.style.overflow = 'hidden';  // firefox, chrome
    document.body.scroll = "no"; // ie only
    var bg = $(".fullcover_div_GoogleMap, .fullcover_div7, .fullcover_div5");
    function resizeBackground() {
        /* FIXED HOTEL ADDON POPUP CAN'T SCROLL WHILE MOBILE IN LANDSCAPE ORIENTATION */
        //bg.height($(window).height() + 200);
    }
    $(window).resize(resizeBackground);
    resizeBackground();
}

$(".s3_1_fdb_modify_icon").click(function () {
    $('.slidercss').prop('disabled', true);
    unloadScrollBars();
});

$(".loginclose img").click(function () {
    $('.slidercss').prop('disabled', false);
    reloadScrollBars();
});

$('.modifysearch_white1nn').show('slow', function (e) {
    $(this).trigger('isVisible');
});

/* step 2 tooltips start */
$(document).on('click', '.show_toltip', function (e) {
    if (screen.width <= 1366) {
        var pricetooltip = $(this).next();
        pricetooltip.fadeIn();
        pricetooltip.delay(3000).fadeOut();
    }
});

/* bagage info & meal */
//$(document).on('click', '.show_toltip1', function (e) {
//    var tipsSelector = $(this).find('.s2-1_form_toll_tip_1');
//    tipsSelector.fadeIn();
//    tipsSelector.delay(5000).fadeOut();
//});
/* step 2 price tooltips end */

/* step 3 tooltips start */
//$(document).on('click', '.show_toltip2', function (e) {
//    var pricetooltip = $('.s3_form_toll_tip');
//    pricetooltip.fadeIn();
//    pricetooltip.delay(5000).fadeOut();
//});

// phase 1 temp disable item
$('.searchtab1, .searchtab2, .searchtab3, .searchtab4').on('click', function (e) {
    if (typeof $(this).data('location') != 'undefined') {
        e.stopImmediatePropagation();
        window.location = $(this).data('location');
    }
});

//$('.ars-payment').on('click', function (e) {
//    var hiddenValue = $('#buttonValue');
//    var clickedVal = $(this).val();
//    hiddenValue.val('PaymentNow');
//    $('#checkoutForm').submit();
//});

$('.day, .month, .year').on('change', function (e) {
    var selector = $(this).parents('.s3-1_ff_black_border_box');

    var day = selector.find('.day');
    var month = selector.find('.month');
    var year = selector.find('.year');

    var sMonth = ['4', '6', '9', '11'];
    var bMonth = ['1', '3', '5', '7', '8', '10', '12'];
    var dOption = day.children('option');
    var OptLbl = dOption.first().val() != "" ? 0 : 1;
    var len = dOption.length - parseInt(OptLbl);

    var hInput = selector.find('input[type=hidden]');
    var hInputName = hInput.attr('name');

    var selectedDay = day.val();
    var selectedMonth = month.val();
    var selectedYear = year.val();
    var prefix = getModelPrefix(hInputName);
    var nDate = (selectedMonth != "" && selectedDay != "" && selectedYear != "") ? selectedYear + "/" + selectedMonth + "/" + selectedDay : "";

    // get age here and assign.
    dateField = new Date(nDate);
    if (hInputName.search('DOB') != -1) {
        var passengerType = $('input:hidden[name="' + prefix + 'PassengerType"]').val();

        var today = typeof (ServerEndDateTime) === 'undefined' ? ServerDateTime : ServerEndDateTime;
        var age = passengerType == "INF" ? (moment(today).diff(moment(dateField), 'months') >= 0 ? moment(today).diff(moment(dateField), 'months') : '-')
            : moment(today).diff(moment(dateField), 'years');
        if (hInputName.toLowerCase().indexOf('dob') > 0 && !isNaN(age)) {
            var ageInput = $("[name='" + prefix + 'Age' + "']");
            ageInput.val(age);
        }
        else {
            var ageInput = $("[name='" + prefix + 'Age' + "']");
            ageInput.val('');
        }
    }

    if (dateField != 'Invalid Date') {
        hInput.val(nDate);
        hInput.trigger('change');
    } else {
        hInput.val('');
    }

    // valid date hidden input field 
    $('input[name*="' + prefix + 'DOB"]').valid();
    if ($('input[name*="' + prefix + 'PassportExpiryDate"]').length) {
        $('input[name*="' + prefix + 'PassportExpiryDate"]').valid();
    }

    function rDays(dRange) {
        dOption.each(function (i) {
            var lValue = i + 1;
            if (i >= dRange) {
                //alert('Removing ' + lValue + '' );
                day.children('option[value=' + lValue + ']').remove();
            }
        });

        for (var i = len; i < dRange; i++) {
            var lValue = i + 1;
            //alert('this is adding ' + lValue);
            day.append("<option value='" + lValue + "'>" + lValue + "</option>");
        }
    }

    if (selectedMonth == 2) {
        var isLeap = leapYear(year.val());
        var dRange = (isLeap == true) && (isLeap != undefined) ? "29" : "28";

        rDays(dRange);
    }
    else if ($.inArray(selectedMonth, sMonth) != -1) {
        var dRange = 30;
        rDays(dRange);
    }
    else if ($.inArray(selectedMonth, bMonth) != -1) {
        var dRange = 31;
        rDays(dRange);
    }
});


function leapYear(year) {
    return ((year % 4 == 0) && (year % 100 != 0)) || (year % 400 == 0);
}

//Fix default date validation (data-val-date) issue
$.validator.addMethod('date',
        function (value, element, params) {
            if (this.optional(element)) {
                return true;
            }

            var ok = true;
            try {
                $.datepicker.parseDate('dd-M-yy', value);
            }
            catch (err) {
                ok = false;
            }
            return ok;
        });

// 2016/12/15 - Temp customize jQuery validator for Register usage START
$.validator.addMethod('dobValid', function (value, element, params) {
    var isValid = true;
    var hiddenValue = $('#DOB').val();
    $("select[name*='user_dob']").each(function (index, element) {
        if ($(element).val() != "" && hiddenValue == "") {
            isValid = false;
            $('[data-valmsg-for$="DOB"]').html("<span>Please select complete date.</span>");
            return false;
        }
    });

    var age = moment(ServerDateTime).diff(moment(new Date(hiddenValue)), 'years');
    if (age < 16) {
        $('[data-valmsg-for$="DOB"]').html("<span>To register you must greater than " + 16 + " years old.</span>");
        isValid = false;
    }
    if (isValid) {
        $('[data-valmsg-for$="DOB"]').html("");
    }
    return isValid;
}, 'Please select a valid date.');

$.validator.addMethod('passportValid', function (value, element, params) {
    var isValid = true;
    var hiddenValue = $('#PassportExpiryDate').val();
    $("select[name*='passport_expire']").each(function (index, element) {
        if ($(element).val() != "" && hiddenValue == "") {
            isValid = false;
            $('[data-valmsg-for$="PassportExpiryDate"]').html("<span>Please select complete date.</span>");
            return false;
        }
    });

    if (new Date(hiddenValue) < ServerDateTime) {
        var sDate = $.datepicker.formatDate('dd M yy', ServerDateTime);
        $('[data-valmsg-for$="PassportExpiryDate"]').html("<span>Selected date must greater than " + sDate + ".</span>");
        isValid = false;
    }
    if (isValid) {
        $('[data-valmsg-for$="PassportExpiryDate"]').html("");
    }
    return isValid;
}, 'Please select a valid date.');
// 2016/12/15 - Temp customize jQuery validator for Register usage END

// Temp customize jQuery validator for Update profile usage START
$.validator.addMethod('profiledobValid', function (value, element, params) {
    var isValid = true;
    var hiddenValue = $('#DOB').val();

    $("select[name*='DOB']").each(function (index, element) {
        if ($(element).val() != "" && hiddenValue == "") {
            isValid = false;
            $('[data-valmsg-for$="DOB"]').html("<span>Please select complete date.</span>");
            return false;
        }
    });
    var age = moment(ServerDateTime).diff(moment(new Date(hiddenValue)), 'years');
    if (age < 16) {
        $('[data-valmsg-for$="DOB"]').html("<span>To register you must greater than " + 16 + " years old.</span>");
        isValid = false;
    }
    if (isValid) {
        $('[data-valmsg-for$="DOB"]').html("");
    }
    return isValid;
}, 'Please select a valid date.');

$.validator.addMethod('profilepassportValid', function (value, element, params) {
    var isValid = true;
    var hiddenValue = $('#PassportExpiryDate').val();

    $("select[name*='PasspExp']").each(function (index, element) {
        if ($(element).val() != "" && hiddenValue == "") {
            isValid = false;
            $('[data-valmsg-for$="PassportExpiryDate"]').html("<span>Please select complete date.</span>");
            return false;
        }
    });
    if (new Date(hiddenValue) < ServerDateTime) {
        var sDate = $.datepicker.formatDate('dd M yy', ServerDateTime);
        $('[data-valmsg-for$="PassportExpiryDate"]').html("<span>Selected date must greater than " + sDate + ".</span>");
        isValid = false;
    }
    if (isValid) {
        $('[data-valmsg-for$="PassportExpiryDate"]').html("");
    }
    return isValid;
}, 'Please select a valid date.');
// Temp customize jQuery validator for Update profile usage END

$(".ffc2_r2_b5").mouseover(function () {
    $(this).find(".yellow_toll_tip").show();
});

$(".ffc2_r2_b5").mouseout(function () {
    $(this).find(".yellow_toll_tip").hide();
});

$(".searchtab1").click(function () {
    $(".tapto_search").slideUp();
    $(".taptosearch_tick").slideUp();
    $(".sir_tabresult").slideDown();
});

$(".searchtab2").click(function () {
    $(".tapto_search").slideUp();
    $(".taptosearch_tick").slideUp();
    $(".sir_tabresult").slideDown();
});

$('input + .clearinput, .clearinput + input').on('click', function (e) {
    e.target.previousElementSibling.value = '';
});

$('.toggle-code').on('click', function (e) {
    //$('.pCode-lblArea').toggle('left');
    $('.pCode-inputArea').toggle('right');
    $('#promo-code').focus();
});

var focusToHiddenField = function (element) {
    if (!$(element).is(":visible")) {
        var parentElement = element.parentElement;
        smoothScroll(parentElement);
    }
};