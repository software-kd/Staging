//Hotel Function - START
var clearGuest1 = function () {
    var length = $('#roomChoise').children('option').length;
    for (i = 0; i < length; i++) {
        if ($('#RoomDetails_' + i + '__Title').prop('selectedIndex') == $('#ContactPerson_Title').prop('selectedIndex')) {
            $('#RoomDetails_' + i + '__Title').prop('selectedIndex', "0");
        }
        if ($('#RoomDetails_' + i + '__GivenName').val() == $('#ContactPerson_GivenName').val()) {
            $('#RoomDetails_' + i + '__GivenName').val('');
        }
        if ($('#RoomDetails_' + i + '__Surname').val() == $('#ContactPerson_Surname').val()) {
            $('#RoomDetails_' + i + '__Surname').val('');
        }
        if ($('#RoomDetails_' + i + '__DateOfBirth_Day').val() == $('#ContactPerson_DOBDays').val()) {
            $('#RoomDetails_' + i + '__DateOfBirth_Day').val('');
        }
        if ($('#RoomDetails_' + i + '__DateOfBirth_Month').val() == $('#ContactPerson_DOBMonths').val()) {
            $('#RoomDetails_' + i + '__DateOfBirth_Month').val('');
        }
        if ($('#RoomDetails_' + i + '__DateOfBirth_Year').val() == $('#ContactPerson_DOBYears').val()) {
            $('#RoomDetails_' + i + '__DateOfBirth_Year').val('');
        }
        $('#RoomDetails_' + i + '__GivenName').blur();
    }
}
var chckRoom1 = function () {
    var index = $('option:selected', '#roomChoise').index();
    if (index != 0) {
        $("#checkbox4").prop('checked', false);
    }
    $('#RoomDetails_' + index + '__Title').prop('selectedIndex', $('#ContactPerson_Title').prop('selectedIndex'));
    $('#RoomDetails_' + index + '__GivenName').val($('#ContactPerson_GivenName').val());
    $('#RoomDetails_' + index + '__Surname').val($('#ContactPerson_Surname').val());
    $('#RoomDetails_' + index + '__DateOfBirth_Day').val($('#ContactPerson_DOBDays').val());
    $('#RoomDetails_' + index + '__DateOfBirth_Month').val($('#ContactPerson_DOBMonths').val());
    $('#RoomDetails_' + index + '__DateOfBirth_Year').val($('#ContactPerson_DOBYears').val());
    $('#RoomDetails_' + index + '__GivenName').blur();
    $('#RoomDetails_' + index + '__DateOfBirth_Day').trigger('change');
}
var bindGuestInfo = function () {
    var length = $('#roomChoise').children('option').length;
    for (i = 1; i < length; i++) {
        $('#RoomDetails_' + i + '__Title').prop('selectedIndex', $('#RoomDetails_0__Title').prop('selectedIndex'));
        $('#RoomDetails_' + i + '__GivenName').val($('#RoomDetails_0__GivenName').val())
        $('#RoomDetails_' + i + '__Surname').val($('#RoomDetails_0__Surname').val())
        $('#RoomDetails_' + i + '__DateOfBirth_Day').val($('#RoomDetails_0__DateOfBirth_Day').val())
        $('#RoomDetails_' + i + '__DateOfBirth_Month').val($('#RoomDetails_0__DateOfBirth_Month').val())
        $('#RoomDetails_' + i + '__DateOfBirth_Year').val($('#RoomDetails_0__DateOfBirth_Year').val())
        $('#RoomDetails_' + i + '__SpecialRequest_AdditionalRequest').val($('#RoomDetails_0__SpecialRequest_AdditionalRequest').val())
        $('#RoomDetails_' + i + '__GivenName').trigger('blur');
        $('#RoomDetails_' + i + '__DateOfBirth_Day').trigger('change');
    }
}
var clearAllApply = function () {
    var length = $('#roomChoise').children('option').length;
    for (i = 1; i < length; i++) {
        $('#RoomDetails_' + i + '__Title').prop('selectedIndex', $('#RoomDetails_0__Title').prop('selectedIndex'));
        $('#RoomDetails_' + i + '__GivenName').val('')
        $('#RoomDetails_' + i + '__Surname').val('')
        $('#RoomDetails_' + i + '__DateOfBirth_Day').val('')
        $('#RoomDetails_' + i + '__DateOfBirth_Month').val('')
        $('#RoomDetails_' + i + '__DateOfBirth_Year').val('')
        $('#RoomDetails_' + i + '__SpecialRequest_AdditionalRequest').val('')
        $('#RoomDetails_' + i + '__GivenName').blur();
    }
}
//Hotel Function - END

//Flight Function - START
var contactPersonIdList = ['#ContactPerson_GivenName', '#ContactPerson_Surname', '#ContactPerson_Email', '#ContactPerson_Phone1'];
var contactPersonId = '#ContactPerson_GivenName, #ContactPerson_Surname, #ContactPerson_Email, #ContactPerson_Phone1, #ContactPerson_DOBDays,#ContactPerson_DOBMonths,#ContactPerson_DOBYears,#ContactPerson_CountryCode,#ContactPerson_Title';
var isGATE = typeof FltSup != "undefined" && (FltSup == "GATE_Int" || FltSup == "GATE_Chn");
var isAirAsia = typeof FltSup != "undefined" && FltSup == "AirAsia";

var bindContactToGuest = function () {
    $('#TravellerDetails_0__GivenName').val($('#ContactPerson_GivenName').val());
    $('#TravellerDetails_0__Surname').val($('#ContactPerson_Surname').val());
    $('#TravellerDetails_0__Title').val($('#ContactPerson_Title').val());
    $('#TravellerDetails_0__Title').prop('selectedIndex', $('#ContactPerson_Title').prop('selectedIndex'));
    $('#TravellerDetails_0__DOBDays').val($('#ContactPerson_DOBDays').val());
    $('#TravellerDetails_0__DOBMonths').val($('#ContactPerson_DOBMonths').val());
    $('#TravellerDetails_0__DOBYears').val($('#ContactPerson_DOBYears').val());
    $('#TravellerDetails_0__Nationality').val($('#ContactPerson_Nationality').val());
    $('#TravellerDetails_0__Age').val($('#ContactPerson_Age').val());
    $('#TravellerDetails_0__DOBMonths').trigger('change');
    $("[name*='TravellerDetails'][name$='Nationality']").trigger('change');
    $("#TravellerDetails_0__PassportNumber").val($('#PassportNo').val());
    $("#TravellerDetails_0__PassportExpiryDateDays").val($('#PassportExpdate').val());
    $("#TravellerDetails_0__PassportExpiryDateMonths").val($('#PassportExpmonth').val());
    $("#TravellerDetails_0__PassportExpiryDateYears").val($('#PassportExpyear').val());
    $("#TravellerDetails_0__PassportExpiryDateMonths").trigger('change');
    $("#TravellerDetails_0__PassportIssueCountry").val($('#PassportCountry').val());
}
var clearFltGuest1 = function () {
    $('#TravellerDetails_0__GivenName').val('');
    $('#TravellerDetails_0__Surname').val('');
    $('#TravellerDetails_0__Title').val($("#TravellerDetails_0__Title option:first").val());
    $('#TravellerDetails_0__Nationality').val('MYS');
    $("#TravellerDetails_0__DOBDays option").attr('selected', false);
    $("#TravellerDetails_0__DOBMonths option").attr('selected', false);
    $("#TravellerDetails_0__DOBYears option").attr('selected', false);
    $('#TravellerDetails_0__Age').val('');
    $("#TravellerDetails_0__PassportNumber").val('');
    $("#TravellerDetails_0__PassportExpiryDateDays option").attr('selected', false);
    $("#TravellerDetails_0__PassportExpiryDateMonths option").attr('selected', false);
    $("#TravellerDetails_0__PassportExpiryDateYears option").attr('selected', false);
    $("#TravellerDetails_0__PassportIssueCountry").val('');
    $('.month').trigger('change');
}
var passportValidation = function () {
    var msg = [];
    $('input[name$=".PassportExpiryDate"][type="hidden"]').each(function (index, element) {
        var passportDate = $(element).val();
        var prefix = getModelPrefix(element.name);
        if (passportDate.length) {
            var tripDate = typeof ServerDateTime == 'undefined' ? $('#tripBeginDate').val() : ServerDateTime;
            if (moment(passportDate, 'YYYY/M/D', true) <= moment(tripDate, 'YYYY/M/D', true).add(6, 'M')) {
                var pName = $('input[name="' + prefix + 'GivenName"').val() + ' ' + $('input[name="' + prefix + 'Surname"').val();
                var parsePassDate = moment(passportDate, 'YYYY/M/D', true).format('DD MMM YYYY');
                msg.push(parsePassDate + ' - ' + pName);
            }
        }
    });
    return msg;
}
//Flight Function - END

$(function () {
    $(document).ready(function (e) {
        history.pushState('', '', window.location.href);

        //Add Validator - START
        $.validator.addMethod("checkSameName", function (value, element, params) {
            var prefix = getModelPrefix(element.name);
            var name = $('input[name="' + prefix + 'GivenName"]').val() + $('input[name="' + prefix + 'Surname"]').val();
            var prefixstart = prefix.substr(0, prefix.indexOf('['));
            name = name.toLowerCase();
            for (var i = 0; i < $('.booking-form').length - 1; i++) {
                var preName = $('#' + prefixstart + '_' + i + '__GivenName').val() + $('#' + prefixstart + '_' + i + '__Surname').val();
                var cur = getModelPrefix($('#' + prefixstart + '_' + i + '__GivenName').prop('name'));
                preName = preName.toLowerCase();
                if (cur !== prefix && name == preName) {
                    return false;
                }
                else {
                }
                //break;
            }
            return true;
        }
        , function (value, element, params) {
            return "Guest Name cannot be same."
        });

        $.validator.addMethod("agreetnc", function (value, element, params) {
            return value;
        }
        , function (value, element, params) {
            return $.validator.format("Please check the terms and condition to register.")
        });

        if (!String.prototype.includes) {
            String.prototype.includes = function () {
                'use strict';
                return String.prototype.indexOf.apply(this, arguments) !== -1;
            };
        }

        $.validator.addMethod("agevalid", function (value, element, params) {
            var isValid = false;
            var prefix = getModelPrefix(element.name);

            if (this.optional(element)) {
                return true;
            }

            if (element.name.includes("ContactPerson")) {
                if (element.name == "ContactPerson.DOB") {
                    isValid = calAge(value) >= 18;
                }
            } else if (element.name.includes("TravellerDetails")) {
                if ($('input[name="' + prefix + 'Age"]').length > 0) {
                    age = $('input[name="' + prefix + 'Age"]').val();
                    passengerType = $('input:hidden[name="' + prefix + 'PassengerType"]').val();
                    if (passengerType == "ADT") {
                        isValid = age >= 12;
                    }
                    else if (passengerType == "CNN") {
                        isValid = age >= 2 && age < 12;
                    }
                    else if (passengerType == "INF") {
                        //This is months
                        isValid = age >= 0 && age <= 23;
                    }
                }
            } else {
                isValid = calAge(value) >= 17;
            }

            return isValid && new Date(value) <= ServerDateTime;
        }
        , function (value, element, params) {
            var prefix = getModelPrefix(element.name);
            var errMsg = "";

            if (element.name == "ContactPerson.DOB") {
                errMsg = "Contact Person age must be >18 Years Old.";
            }
            else if (element.name.includes("TravellerDetails")) {
                var age, passengerType;
                age = $('input[name="' + prefix + 'Age"]').val();
                passengerType = $('input:hidden[name="' + prefix + 'PassengerType"]').val();

                if (passengerType == "ADT") {
                    errMsg = "Age for Adult must be >11 Years Old.";
                }
                else if (passengerType == "CNN") {
                    errMsg = "Age for Child must be 2-11 Years Old.";
                }
                else if (passengerType == "INF") {
                    errMsg = "Age for Infant must be 0-23 Months Old (Under 2 Years Old).";
                }
                else if (element.name == "BookingContactPerson.DOB") {
                    errMsg = "Contact Person age must be >17 Years Old.";
                }
                else {
                    errMsg = "Invalid age.";
                }
            }
            else {
                errMsg = "Room Guest age must be >17 Years Old.";
            }

            return $.validator.format(errMsg);
        });

        $.validator.addMethod("passportdate", function (value, element, params) {
            var passportDate, tripDate;
            if (this.optional(element)) {
                return true;
            }
            passportDate = value;
            tripDate = typeof ServerDateTime == 'undefined' ? $('#tripBeginDate').val() : ServerDateTime;

            return moment(passportDate, 'YYYY/M/D', true) >= moment(tripDate, 'YYYY/M/D', true);
        }
        , function (value, element, params) {
            var tripDate = typeof ServerDateTime == 'undefined' ? $('#tripBeginDate').val() : ServerDateTime;
            var parseDate = moment(tripDate, 'YYYY/M/D', true).format("DD-MMM-YYYY, ddd");
            return $.validator.format("Passport Expiry Date must greater or equal to {0}.", parseDate)
        });

        $.validator.addMethod("passportDateEmpty", function (value, element, params) {
            return value != '';
        }
        , function (value, element, params) {
            return $.validator.format("Passport Expiry Date Is Required")
        });

        $.validator.addMethod("passportNumberEmpty", function (value, element, params) {
            return value != '';
        }
        , function (value, element, params) {
            return $.validator.format("Passport Number Is Required")
        });

        $.validator.addMethod("namecombinaation", function (value, element, params) {
            var prefix = getModelPrefix(element.name);
            var givenname = $('input[name="' + prefix + 'GivenName"]').val().length;
            var surname = $('input[name="' + prefix + 'Surname"]').val().length;
            if (givenname + surname > 54) {
                return false;
            }
            else {
                return true;
            }
        }
        , function (value, element, params) {
            return 'Total length of the First Name and Family Name cannot more than 54 characters.'
        });

        $.validator.methods.date = function (value, element, params) {
            var isValid = value != '' ? moment(value, 'YYYY/M/D', true).isValid() : true;
            var name = typeof element.name != 'undefined' ? element.name : '';
            var prefix = getModelPrefix(name);
            var modelName = getModelName(element.name);

            switch (modelName) {
                case "DateOfBirth":
                    var selectDOB = $('select[name*="' + prefix + 'DateOfBirth"]').map(function (i, e) {
                        return $(e).val();
                    }).get();
                    var isValidDOB = (selectDOB[0] != '' && selectDOB[1] != '' && selectDOB[2] != '') || (selectDOB[0] == '' && selectDOB[1] == '' && selectDOB[2] == '');
                    isValid = isValid && isValidDOB;
                    break;
                case "DOB":
                    var selectDOB = $('select[name*="' + prefix + 'DOB"]').map(function (i, e) {
                        return $(e).val();
                    }).get();
                    var isValidDOB = (selectDOB[0] != '' && selectDOB[1] != '' && selectDOB[2] != '') || (selectDOB[0] == '' && selectDOB[1] == '' && selectDOB[2] == '');
                    isValid = isValid && isValidDOB;
                    break;
                case "PassportExpiryDate":
                    var selectPassport = $('select[name*="' + prefix + 'PassportExpiryDate"]').map(function (i, e) {
                        return $(e).val();
                    }).get();
                    var isValidPassport = (selectPassport[0] != '' && selectPassport[1] != '' && selectPassport[2] != '') || (selectPassport[0] == '' && selectPassport[1] == '' && selectPassport[2] == '');
                    isValid = isValid && isValidPassport;
                    break;
                case "_DepartureDate":
                    isValid = true;
                    break;
                default:
                    break;
            }

            return isValid;
        }
        //Add Validator - END

        $("#registerCheckBox").prop('checked', true);

        if ($('#registerCheckBox').prop('checked')) {
            $(".s3_optional_register").slideToggle();
            $("#MemberRegisterModels_AgreeTnC").rules("add", "agreetnc");
        }
        else {
            $("[name$=Password]").rules("remove", "required");
        }

        $('#ContactPerson_GivenName').rules('add', 'namecombinaation');
        $('#TravellerDetails_0__GivenName').rules('add', 'namecombinaation');

        //for more than 1 guest room
        var numberOfRooms = $('#roomChoise').children('option').length;
        for (var i = 0; i < numberOfRooms; i++) {
            $('#RoomDetails_' + i + '__GivenName').rules('add', 'namecombinaation');
        }

        toggleOptionalRegister($.checkRegister($('#ContactPerson_Email').val()));

        checkValidWhenStart();

        window.onload = function () {
            var NationalityInput = $("[name*='TravellerDetails'][name$='Nationality']");
            NationalityInput.each(function (i, e) {
                if (NationalityInput.eq(i).val() == "" && isdomestic && !isGATE) {
                    $("[name*='TravellerDetails'][name$='PassportNumber']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDate']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateDays']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateMonths']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateYears']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportIssueCountry']").eq(i).val("");
                    $("#passpno_" + i).hide();
                    $("#passpexp_" + i).hide();
                    $("#passpcountry_" + i).hide();
                    $("#icno_" + i).show();
                    $("#icno_" + i).addClass("goup");

                    var marginForNRIC = "-100px";
                    if ($("[id*='TravellerDetails'][id$='Nationality-error']").eq(i).length > 0 && isAirAsia && $(window).width() > 500) {
                        marginForNRIC = "-134px";
                    } else if (isAirAsia && $(window).width() <= 500) {
                        marginForNRIC = "-10px";
                    } else {
                        marginForNRIC = "-100px";
                    }
                    $("#icno_" + i).css("margin-top", marginForNRIC);

                }

                $("input[id*='TravellerDetails'][name$='Nationality']").eq(i).val(NationalityInput.eq(i).val());
            });
        }

        $("[name*='TravellerDetails'][name$='Nationality'], [name='ContactPerson.Country']").on('change', function () {
            var NationalityInput = $("[name*='TravellerDetails'][name$='Nationality']");
            NationalityInput.each(function (i, e) {
                if ((NationalityInput.eq(i).val() == "MYS" || NationalityInput.eq(i).val() == "") && isdomestic && !isGATE) {
                    $("[name*='TravellerDetails'][name$='PassportNumber']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDate']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateDays']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateMonths']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateYears']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportIssueCountry']").eq(i).val("");
                    $("#passpno_" + i).hide();
                    $("#passpexp_" + i).hide();
                    $("#passpcountry_" + i).hide();
                    $("#icno_" + i).show();
                    $("#icno_" + i).addClass("goup");

                    //Temporary fix this, need to think a better way to solve this
                    if (isAirAsia && NationalityInput.eq(i).val() == "" && $(window).width() > 500) {
                        marginForNRIC = "-134px";
                    } else if (isAirAsia && NationalityInput.eq(i).val() == "" && $(window).width() <= 500) {
                        marginForNRIC = "-10px";
                    }
                    else {
                        marginForNRIC = "-100px";
                    }
                    $("#icno_" + i).css("margin-top", marginForNRIC);
                }
                else {
                    $("[name*='TravellerDetails'][name$='IdentityNumber']").eq(i).val("");
                    $("#icno_" + i).hide();
                    $("#icno_" + i).removeClass("goup");
                    $("#passpno_" + i).show();
                    $("#passpexp_" + i).show();
                    $("#passpcountry_" + i).show();
                }

                $("input[id*='TravellerDetails'][name$='Nationality']").eq(i).val(NationalityInput.eq(i).val());
            });
        });

        var checkNationality = function () {
            var NationalityInput = $("[name*='TravellerDetails'][name$='Nationality']");
            NationalityInput.each(function (i, e) {
                if (isdomestic && !isGATE && NationalityInput.eq(i).val() == "MYS") {
                    $("[name*='TravellerDetails'][name$='PassportNumber']").eq(i).val("");
                    $("#passpno_" + i).hide();
                    $("[name*='TravellerDetails'][name$='PassportExpiryDate']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateDays']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateMonths']").eq(i).val("");
                    $("[name*='TravellerDetails'][name$='PassportExpiryDateYears']").eq(i).val("");
                    $("#passpexp_" + i).hide();
                    $("[name*='TravellerDetails'][name$='PassportIssueCountry']").eq(i).val("");
                    $("#passpcountry_" + i).hide();
                    $("#icno_" + i).addClass("goup");

                    var marginForNRIC = "-100px";
                    if ($("[id*='TravellerDetails'][id$='Nationality-error']").eq(i).length > 0 && isAirAsia && $(window).width() > 500) {
                        marginForNRIC = "-134px";
                    } else if (isAirAsia && $(window).width() <= 500) {
                        marginForNRIC = "-10px";
                    } else {
                        marginForNRIC = "-100px";
                    }
                    $("#icno_" + i).css("margin-top", marginForNRIC);
                }
            });
        }
    });

    var checkValidWhenStart = function () {
        $('#ContactPerson_GivenName').valid();
        $('#ContactPerson_Surname').valid();
        $('#ContactPerson_Email').valid();
        $('#ContactPerson_Phone1').valid();
    };

    var form = $('#checkoutDetailForm');
    var formValidator = form.validate();
    formValidator.settings.ignore = ''; // validate hidden fields

    var validateForm = function () {

        var noerror = form.valid();

        if (noerror) {
            checkMarker();
        }

        if (formValidator.errorList.length !== 0) {
            var dfd = $.Deferred();
            var errElement = formValidator.errorList[0].element;

            checkMarker();

            $('.booking-form').each(function (i, e) {
                var fi = $(e).find(errElement);
                var isElementExists = fi.length != 0;
                var formId = $(e).attr('id');

                var nowFocusForm = $('.s3-1_guestdetail_box > ul li[value="' + formId + '"]');
                var nowFocusBar = $(".contact_detail_heading_small[value='" + formId + "']");

                if (isElementExists) {
                    // desktop view
                    $('.s3-1_guestdetail_box > ul li[value!="' + formId + '"]').removeClass('s3-1_activli');
                    $('.s3-1_guestdetail_box > ul li[value="' + formId + '"]').addClass('s3-1_activli');
                    $('.booking-form[id!="' + formId + '"]').hide();
                    $(e).show();

                    // mobile tab
                    $('.chhc_bar').removeClass('cdhc_redbar');
                    $(nowFocusBar).find('.chhc_bar').addClass('cdhc_redbar');

                    dfd.resolve();
                    // return false to end each looping
                    return false;
                }
            });
            dfd.done(function (e) {
                formValidator.focusInvalid();

                //Focus on hidden field like DOB -> focus not working on hidden, so focus to parent element
                var errorElement = formValidator.errorList[0].element;
                focusToHiddenField(errorElement);
            });
        }
        return noerror;
    }
    var focusNextGuestForm = function () {
        var next = $(".s3-1_guestdetail_box ul li.s3-1_activli").next();
        var curr_tab = $(".s3-1_guestdetail_box ul li.s3-1_activli").attr('value');
        var tab_id = next.attr('value');
        if (curr_tab === 'contactform') {
            if (typeof GTM_trackAddToCart !== 'undefined') {
                trackCheckout1(GTM_trackAddToCart);
            }
        }
        if (tab_id !== 'contactform') {
            $(".s3-1_guestdetail_box ul li").removeClass("s3-1_activli");
            next.addClass("s3-1_activli");
            $('.booking-form[id!="' + tab_id + '"]').hide();
            $("#" + tab_id).fadeIn();

            // mobile tab
            var mob_tab = $('.contact_detail_heading_small[value="' + tab_id + '"]');
            $('.chhc_bar').removeClass('cdhc_redbar');
            mob_tab.find('.chhc_bar').addClass('cdhc_redbar');

            if ($(window).width() > 900) {
                smoothScroll($('.s3-1_guestdetail_box ul'));
            }
            else {
                smoothScroll(mob_tab);
            }
        }
    }
    var toggleOptionalRegister = function (display) {
        var chkBox = $('#registerCheckBox');
        if (display) {
            $(".check_create_acc").show();
        }
        else {
            $('#registerCheckBox').prop('checked', false);
            $(".s3_optional_register, .check_create_acc").hide();
            $("[name$=Password]").rules("remove", "required");
            $("#MemberRegisterModels_AgreeTnC").rules("remove", "agreetnc");
        }
    }
    var checkIdFieldIsNull = function (idArray) {
        var isEmptyField = true;
        for (i = 0; i < idArray.length; i++) {
            if ($(idArray[i]).val() != '') {
                isEmptyField = false;
            } else {
                isEmptyField = true;
                break;
            }
        }
        return isEmptyField;
    }
    var focusError = function (element) {
        $('html, body').animate({
            scrollTop: $(element).offset().top
        }, 300);
    };
    var toggleSubmitBtn = function () {
        form.valid();
        var errList = formValidator.errorList;
        var btn = $('#guest-submit-btn');
        if (errList.length == 0) {
            btn.attr('type', 'submit');
            btn.removeClass('incompleted');
        }
        else if (errList.length > 0) {
            btn.attr('type', 'button');
            btn.addClass('incompleted');
        }
    };
    var checkMarker = function (validNow) {
        validNow = typeof validNow == "undefined" ? true : validNow;
        if (validNow) {
            toggleSubmitBtn();
        }
        var errList = formValidator.errorList;

        var errForm = [];
        for (var i = 0; i < errList.length; i++) {
            var errElement = errList[i].element;

            var formId = $('.booking-form').has(errElement).attr('id');
            if ($.inArray(formId, errForm) == -1) {
                errForm.push(formId);
            }
            var nowForm = $('.s3-1_guestdetail_box > ul li[value="' + formId + '"]');
            var nowBar = $('.contact_detail_heading_small[value="' + formId + '"]');

            nowForm.find('.completed').remove();
            nowBar.find('.completed').remove();
        }

        $('.booking-form').each(function (i, e) {
            var postbackError = $(this).has('.field-validation-error').length;
            if ($.inArray(this.id, errForm) == -1 && postbackError === 0) {
                var g = $('.s3-1_guestdetail_box > ul li[value="' + this.id + '"]');
                var c = $('.contact_detail_heading_small[value="' + this.id + '"]');
                if (g.has('.completed').length == 0) {
                    g.append('<span class="completed"></span>');
                    g.find('.igbguest').hide();
                }
                if (c.has('.completed').length == 0) {
                    c.find('.chhc_bar').prepend('<span class="completed">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span>');
                }
            }
        });
    }
    var checkOneFormAllValid = function (prefix, bookingFormId) {
        toggleSubmitBtn();
        if (prefix.indexOf("SpecialRequest")) {
            prefix = prefix.replace("SpecialRequest.", "")
        }

        if (prefix.length) {
            var isValid = $("input[name*='" + prefix + "']").valid();
            var g = $('.s3-1_guestdetail_box > ul li[value="' + bookingFormId + '"]');
            var c = $('.contact_detail_heading_small[value="' + bookingFormId + '"]');

            if (isValid) {
                if (g.has('.completed').length == 0) {
                    g.append('<span class="completed"></span>');
                    g.find('.igbguest').hide();
                    g.find('#igb3').remove();
                }
                if (c.has('.completed').length == 0) {
                    //c.find('.chhc_bar').prepend('<span class="completed"></span>');
                    c.find('.chhc_bar').prepend('<span class="completed" >&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span>'); //<img src="../images/tick_done.png" />
                    c.find('#igb3').remove();
                }
            }
            else {
                g.find('.completed').remove();
                g.find('.igbguest').show();
                c.find('.completed').remove();
            }
        }
    }
    var markAllValid = function () {
        $('.s3-1_guestdetail_box > ul li').find('.completed').remove();
        $(".contact_detail_heading_small").find('.completed').remove();
        $('.s3-1_guestdetail_box > ul li').append('<span class="completed"></span>');
        $(".contact_detail_heading_small").find('.chhc_bar').prepend('<span class="completed"></span>');
    }
    var checkContactPersonIdList = function () {
        var isEmpty = checkIdFieldIsNull(contactPersonIdList);
    }
    var bindContact = function () {
        checkContactPersonIdList(contactPersonIdList);

        var imFlying = $("#checkbox2").is(':checked');
        if (imFlying) {
            bindContactToGuest();
        }
    };
    $(contactPersonId).on("blur", function (event) {
        bindContact();
    });

    $.checkRegister = function (e) {
        var params = { Email: e };
        var result = false;
        $.ajax({
            url: '/Member/IsEmailAvailable' + '?' + $.param(params),
            method: 'GET',
            async: false,
            cache: false,
            contentType: "json",
        }).then(function (res) {
            result = res;
        });
        return result;
    };

    // preparation for valid without submit
    form.on('submit', function (e) {
        if (validateForm()) {
            if (typeof sessionStorage.ecomselected == 'undefined') {
                trackCheckout2(GTM_trackAddToCart);
            }
            var msg = passportValidation();
            if (msg.length && !$('#popup-modal').is(':visible')) {
                $('#popup-modal').show();
                $('#modal-container').html("<div class='session_lb_text'>Notice</div><div class='session_lb_text1' style='text-align: justify;font-family: initial;'>Generally all travellers passports must have the min. validity of 6 months from the departure date. Therefore, please be reminded to renew your passport before your trip. You may check with your local immigration body for more details.</div>");
                $('#modal-container > .session_lb_text1').append('<br/>');
                $('#modal-container > .session_lb_text1').append('<br/>Guest require attention: ');
                $(msg).each(function (i, value) {
                    $('#modal-container > .session_lb_text1').append('<span style="text-transform:uppercase;color:#ff0000;"><br/>' + value + '</span>');
                });
                $('#modal-container > .session_lb_text1').append("<div class='btn-section'><a href='javascript:;' class='continue-submit'><div class='redbacktohome_button modal-confirm-btn''>Continue</div></a><a href='javascript:;' class='modal-close'><div class='redbacktohome_button modal-cancel-btn'>Cancel</div></a></div>");
                e.preventDefault();
            }
            else {
                if (typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}') {
                    ecomcheckout(2);
                }
            }
            $('#btnSubmitForm[type="submit"]').attr('disabled', 'disabled');
            $('#btnSubmitForm[type="submit"]').css('background-color', '#b1a9a9 !important', 'cursor', 'default');
            $('#btnSubmitForm[type="submit"] span').text('Please wait...');
            $('form .btn-loadstate').show();
        }
    });
    $(document).on('click', '#guest-btn', function (e) {
        //Continue as guest -- START
        e.preventDefault();
        $(".fullcover_div").fadeOut();
        $("body").removeClass("modal-open");
        //Continue as guest -- END
    })
    $('input').on('blur', function (e) {
        var prefix = getModelPrefix($(this).attr('name'));
        var bookingFormId = $('.booking-form').has(this).attr('id');
        checkOneFormAllValid(prefix, bookingFormId);
    });
    $('.s3-1_form_button[type="button"]').on('click', function (e) {
        var sectionId = $(this).parents('.booking-form').attr('id');
        var validator = $('#checkoutDetailForm').validate();
        $('#' + sectionId + ' *input, #' + sectionId + ' *select').valid();
        if (validator.errorList.length) {
            validator.focusInvalid();

            //Focus on hidden field like DOB -> focus not working on hidden, so focus to parent element
            var errorElement = validator.errorList[0].element;
            focusToHiddenField(errorElement);
        }
        else {
            checkMarker(true);
            focusNextGuestForm();
        }
    });
    $('#checkoutDetailForm').on('change', 'input[type="hidden"]', function (e) {
        var prefix = getModelPrefix($(this).attr('name'));
        var bookingFormId = $('.booking-form').has(this).attr('id');
        checkOneFormAllValid(prefix, bookingFormId);
    });
    $('#ContactPerson_Email').on('blur', function (event) {
        toggleOptionalRegister($.checkRegister(this.value));
    });
    $("#checkbox3 ,#checkbox2").on('change', function (event) {
        var imFlying = $(this).is(':checked');
        var dfd = $.Deferred();
        if (imFlying) {
            bindContactToGuest();
            dfd.resolve();
        } else {
            $('.popup').show();
            $('.modal-container').html("<div class='session_lb_text'>Confirm</div><div class='session_lb_text1'>Guest Information will be REMOVE<br/>Continue proceed?</div><a href='javascript:;' class='clear-guest1'><div class='redbacktohome_button modal-confirm-btn''>Yes</div></a><a href='javascript:;' class='modal-close'><div class='redbacktohome_button modal-cancel-btn'>Cancel</div></a>");
            $(this).prop('checked', true);
        }
        dfd.done(function (e) {
            checkMarker();
        });
    })
    $('.popup').on('click', '.clear-guest1', function (e) {
        var IsFlightBooking = $("[name$=PassengerType]").length > 0;
        if (!IsFlightBooking) {
            clearGuest1();
            $("#checkbox3").prop('checked', false);
        } else {
            clearFltGuest1();
            $("#checkbox2").prop('checked', false);
        }
        $('.popup').hide();
        checkMarker();
    });
    $('#popup-modal').on('click', '.continue-submit', function (e) {
        form.submit();
    });
    $('.s3_create_account').on('click change', function (e) {
        var chkBox = $('#registerCheckBox');
        $('#registerCheckBox').prop('checked', !chkBox.is(':checked'));
        chkBox.trigger('change');
        if (chkBox.is(':checked')) {
            $("[name$=Password]").rules("add", "required");
            $("#MemberRegisterModels_AgreeTnC").rules("add", "agreetnc");
        }
        else {
            $("[name$=Password]").rules("remove", "required");
            $("#MemberRegisterModels_AgreeTnC").rules("remove", "agreetnc");
        }
    });
    $('.s3_create_account').on('keydown', function (e) {
        if (e.keyCode == '32') {
            var chkBox = $('#registerCheckBox');
            $('#registerCheckBox').prop('checked', !chkBox.is(':checked'));
            chkBox.trigger('change');
            if (chkBox.is(':checked')) {
                $("[name$=Password]").rules("add", "required");
                $("#MemberRegisterModels_AgreeTnC").rules("add", "agreetnc");
            }
            else {
                $("[name$=Password]").rules("remove", "required");
                $("#MemberRegisterModels_AgreeTnC").rules("remove", "agreetnc");
            }
        }
    });
    $('#registerCheckBox').on('change', function (e) {
        $(".s3_optional_register").slideToggle();
        if ($('#registerCheckBox').is(':checked')) {
            $('.s3-1_guestdetail_box > ul li[value="contactform"]').find('.completed').remove();
        }
        else {
            if ($('.s3-1_guestdetail_box > ul li[value="contactform"]').hasClass('completed')) {
                $('.s3-1_guestdetail_box > ul li[value="contactform"]').append('<span class="completed"></span>');
            }
        }
    });
    $("#MemberRegisterModels_AgreeTnC").on('change', function (e) {
        $(this).valid();
    })
    $("input#registerCheckBox").on('focus', function (event) {
        $("input[type=checkbox]:not(old) + input + label > span").addClass("focusborder");
    });
    $("input#registerCheckBox").on('focusout', function (event) {
        $("input[type=checkbox]:not(old) + input + label > span").removeClass("focusborder");
    });

    if (typeof FltSup != "undefined" && FltSup == "AirAsia") {
        for (var x = 1; x < $('.booking-form').length - 1; x++) {
            $('#TravellerDetails_' + x + '__Surname').rules('add', 'checkSameName');
        }
    }

    for (var i = 0; i < $('.booking-form').length - 1; i++) {
        var psType = $('input[name="TravellerDetails[' + i + '].PassengerType"]').val();
        var hotelsup = $("#Supplier").val();
        if (psType == "CNN" || psType == "INF" || hotelsup == "HotelBeds") {
            $('input[name="TravellerDetails[' + i + '].DOB"]').rules('add', 'required');
        }

        $('input[name="TravellerDetails[' + i + '].DOB"]').rules('add', 'date');
        $('input[name="TravellerDetails[' + i + '].DOB"]').rules('add', 'agevalid');
        $('input[name="TravellerDetails[' + i + '].PassportExpiryDate"]').rules('add', 'passportdate');
        //$('input[name="TravellerDetails[' + i + '].DOB"]').trigger('change');

        if (typeof FltSup != "undefined" && (FltSup == "GATE_Chn" || FltSup == "GATE_Int")) {
            $('input[name="TravellerDetails[' + i + '].PassportExpiryDate"]').rules('add', 'passportDateEmpty');
            $('input[name="TravellerDetails[' + i + '].PassportNumber"]').rules('add', 'passportNumberEmpty');

        }

        if (typeof FltSup != "undefined" && (FltSup == "AirAsia" || FltSup == "GATE_Int")) {
            if (psType == "ADT") {
                $('input[name="TravellerDetails[' + i + '].DOB"]').rules('add', 'required');
            }
        }
    }
});

$(document).ready(function () {
    $('input[name="ContactPerson.Phone2"], select[name="ContactPerson.Phone2LocationCode"]').on('change', function (e) {
        phoneValidation();
        $('select[name="ContactPerson.Phone2LocationCode"]').valid();
        $('input[name="ContactPerson.Phone2"]').valid();
    });

    var phoneValidation = function () {
        if ($('input[name="ContactPerson.Phone2"]').val().length == '0' && $('select[name="ContactPerson.Phone2LocationCode"]').val().length == '0') {
            $('input[name="ContactPerson.Phone2"]').rules('remove', 'required');
            $('select[name="ContactPerson.Phone2LocationCode"]').rules('remove', 'required');
            $('[data-valmsg-for="ContactPerson.Phone2LocationCode"]').empty();
        }
        else {
            $('input[name="ContactPerson.Phone2"]').rules('add', 'required');
            $('select[name="ContactPerson.Phone2LocationCode"]').rules('add', 'required');
        }
    }

    var form = $('#checkoutDetailForm');
    var formValidator = form.validate();
    formValidator.settings.ignore = '';
    // Contact Person
    $('input[name="ContactPerson.DOB"]').rules('add', 'agevalid');
    //$('input[name*=DOB]').rules('add', 'agevalid');
    var DOBInput = $("input[name*='RoomDetails'][name$='DOB']");
    DOBInput.each(function (i, e) {
        $("input[name*='RoomDetails'][name$='DOB']").eq(i).rules('add', 'agevalid');
    })
    $("#ContactPerson_PostalCode").val($.trim($("#ContactPerson_PostalCode").val()));
});

$("#checkbox4").change(function () {
    if ($("#checkbox4").is(":checked")) {

        var smokeval = $("[name='RoomDetails[0].SpecialRequest.SmokingPreferences']:checked").val();
        $("input[type='radio'][name^='RoomDetails['][name$='].SpecialRequest.SmokingPreferences'][value=" + smokeval + "]").prop('checked', true);

        var bedval = $("[name='RoomDetails[0].SpecialRequest.BetTypeID']:checked").val();
        $("input[type='radio'][name^='RoomDetails['][name$='].SpecialRequest.BetTypeID'][value=" + bedval + "]").prop('checked', true);

        var checkinval = $("[name='RoomDetails[0].CheckInMode']:checked").val();
        $("input[type='radio'][name^='RoomDetails['][name$='].CheckInMode'][value=" + checkinval + "]").prop('checked', true);

        $("[name^='RoomDetails['][name$='].AdditionalRequest']").val($("[name='RoomDetails[0].AdditionalRequest']").val());

        bindGuestInfo()
        return;
    }
    else {
        var length = $('#roomChoise').children('option').length;
        for (i = 1; i < length; i++) {
            $("[name='RoomDetails[" + i + "].SpecialRequest.SmokingPreferences'][value='Either']").prop('checked', true);
            $('[name="RoomDetails[' + i + '].SpecialRequest.BetTypeID"]').off('change').prop('checked', false);
            $("[name='RoomDetails[" + i + "].CheckInMode'][value='Either']").prop('checked', true);
            $('[name="RoomDetails[' + i + '].AdditionalRequest"]').val("");
        }
        clearAllApply()
    }
});
$("[name^='RoomDetails[0]']").change(function () {
    if ($("#checkbox4").is(":checked")) {

        var smokeval = $("[name='RoomDetails[0].SpecialRequest.SmokingPreferences']:checked").val();
        $("input[type='radio'][name^='RoomDetails['][name$='].SpecialRequest.SmokingPreferences'][value=" + smokeval + "]").prop('checked', true);

        var bedval = $("[name='RoomDetails[0].SpecialRequest.BetTypeID']:checked").val();
        $("input[type='radio'][name^='RoomDetails['][name$='].SpecialRequest.BetTypeID'][value=" + bedval + "]").prop('checked', true);

        var checkinval = $("[name='RoomDetails[0].CheckInMode']:checked").val();
        $("input[type='radio'][name^='RoomDetails['][name$='].CheckInMode'][value=" + checkinval + "]").prop('checked', true);

        $("[name^='RoomDetails['][name$='].AdditionalRequest']").val($("[name='RoomDetails[0].AdditionalRequest']").val());

        bindGuestInfo()
        return;
    }
});
$("#checkbox3").on('change', function (event) {
    var ImFlying = $(this).is(':checked');
    var dfd = $.Deferred();
    var index = $('option:selected', '#roomChoise').index();
    if (ImFlying) {
        chckRoom1()
    }
})
$('#roomChoise').on('change', function (event) {
    clearGuest1();
    var ImFlying = $("#checkbox3").is(':checked');
    var dfd = $.Deferred();
    var index = $('option:selected', '#roomChoise').index();
    if (ImFlying) {
        chckRoom1()
    }
})
$('#ContactPerson_Title, #ContactPerson_GivenName, #ContactPerson_Surname, #ContactPerson_DOBDays, #ContactPerson_DOBMonths, #ContactPerson_DOBYears').on("blur", function (event) {
    var ImFlying = $("#checkbox3").is(':checked');
    var index = $('option:selected', '#roomChoise').index();
    if (ImFlying) {
        chckRoom1()
    }
});

$(document).ready(function () {
    var hotelsup = $("#Supplier").val();
    if (hotelsup == "HotelBeds") {
        var length = $('#roomChoise').children('option').length;
        for (i = 0; i < length; i++) {
            $('#RoomDetails_' + i + '__DateOfBirth_Day').change();
        }
    }
});

$("[name$='DateOfBirth.Day'],[name$='DateOfBirth.Month'],[name$='DateOfBirth.Year']").change(function () {
    var hotelsup = $("#Supplier").val();
    var GuestDOBInput = $("input[name*='RoomDetails'][name$='DOB']");
    var prefix = $(this).attr('name');
    var prefixno = prefix.split('[').pop().split(']').shift();
    if ($('#RoomDetails_' + prefixno + '__DateOfBirth_Day').val() != "" || $('#RoomDetails_' + prefixno + '__DateOfBirth_Month').val() != "" || $('#RoomDetails_' + prefixno + '__DateOfBirth_Year').val() != "") {
        GuestDOBInput.eq(prefixno).rules('add', {
            required: true,
            messages: {
                required: "Please fill in complete date",
            }
        });
    } else {
        GuestDOBInput.eq(prefixno).rules('remove', 'required');
        if (hotelsup == "HotelBeds") {
            GuestDOBInput.eq(prefixno).rules('add', {
                required: true,
                messages: {
                    required: "DOB is required",
                }
            });
        }
    }
});

$('[name^="RoomDetails"][name$="GivenName"], [name^="RoomDetails"][name$="Surname"]').blur(function () {
    var hotelsup = $("#Supplier").val();
    if (hotelsup == "Tourplan") {
        for (var x = 1; x < $('.booking-form').length - 1; x++) {
            $('#RoomDetails_' + x + '__Surname').rules('add', 'checkSameName');
        }
    }
});

$(document).ready(function () {
    if ($("#Supplier").val() == "HotelBeds" && $("[name$=PassengerType]").length > 0) {
        $("select[name='ContactPerson.DOBDays']").change();
    }
});

$("[name='ContactPerson.DOBDays'],[name='ContactPerson.DOBMonths'],[name='ContactPerson.DOBYears']").change(function () {
    var ContactDOBInput = $("input[name='ContactPerson.DOB']");
    if ($("[name='ContactPerson.DOBDays']").val() != "" || $("[name='ContactPerson.DOBMonths']").val() != "" || $("[name='ContactPerson.DOBYears']").val() != "") {
        ContactDOBInput.rules('add', {
            required: true,
            messages: {
                required: "Please fill in complete date",
            }
        });
    } else {
        ContactDOBInput.rules('remove', 'required');
        if ($("#Supplier").val() == "HotelBeds" && $("[name$=PassengerType]").length > 0) {
            ContactDOBInput.rules('add', {
                required: true,
                messages: {
                    required: "DOB is required",
                }
            });
        }
    }
});

$("[name*='TravellerDetails'][name$='DOBDays'],[name*='TravellerDetails'][name$='DOBMonths'],[name*='TravellerDetails'][name$='DOBYears']").change(function () {
    var GuestDOBInput = $("input[name*='TravellerDetails'][name$='DOB']");
    var prefix = $(this).attr('name');
    var prefixno = prefix.split('[').pop().split(']').shift();
    if ($('#TravellerDetails_' + prefixno + '__DOBDays').val() != "" || $('#TravellerDetails_' + prefixno + '__DOBMonths').val() != "" || $('#TravellerDetails_' + prefixno + '__DOBYears').val() != "") {
        GuestDOBInput.eq(prefixno).rules('add', {
            required: true,
            messages: {
                required: "Please fill in complete date",
            }
        });
    }
});

$("[name*='TravellerDetails'][name$='PassportExpiryDateDays'],[name*='TravellerDetails'][name$='PassportExpiryDateMonths'],[name*='TravellerDetails'][name$='PassportExpiryDateYears']").change(function () {
    var GuestDOBInput = $("input[name*='TravellerDetails'][name$='PassportExpiryDate']");
    var prefix = $(this).attr('name');
    var prefixno = prefix.split('[').pop().split(']').shift();
    if ($('#TravellerDetails_' + prefixno + '__PassportExpiryDateDays').val() != "" || $('#TravellerDetails_' + prefixno + '__PassportExpiryDateMonths').val() != "" || $('#TravellerDetails_' + prefixno + '__PassportExpiryDateYears').val() != "") {
        GuestDOBInput.eq(prefixno).rules('add', {
            required: true,
            messages: {
                required: "Please fill in complete date",
            }
        });
    }
});

$("select[name$='HotelSpecialRequest.RoomType']").on('change', function (event) {
    var prefix = $(this).attr('name');
    var prefixno = prefix.split('[').pop().split(']').shift();
    var roomtype = $("option:selected", this).index();
    var length = $('.booking-form').length - 1;
    var i = 0;
    $('[name="TravellerDetails[' + prefixno + '].HotelSpecialRequest.BetTypeID"]').off('change').prop('checked', false);
    $(".roomsrdiv_" + prefixno).addClass("hidden");
    $(".roomsrdiv_" + prefixno + ".room_" + roomtype).removeClass("hidden");

    for (i = 0; i < prefixno; i++) {
        if ($("option:selected", "select[name='TravellerDetails[" + i + "].HotelSpecialRequest.RoomType']").index() == roomtype) {
            var smoke = $("[name='TravellerDetails[" + i + "].HotelSpecialRequest.SmokingPreferences']:checked").val();
            var checkin = $("[name='TravellerDetails[" + i + "].HotelSpecialRequest.CheckInMode']:checked").val();
            var addreq = $("[name='TravellerDetails[" + i + "].HotelSpecialRequest.AdditionalRequest']").val();
            var bet = $("[name='TravellerDetails[" + i + "].HotelSpecialRequest.BetTypeID']:checked", ".room_" + roomtype).val();
            $("[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.SmokingPreferences'][value='" + smoke + "']").prop('checked', true);
            $("[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.CheckInMode'][value='" + checkin + "']").prop('checked', true);
            $("[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.AdditionalRequest']").val(addreq);
            $("[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.BetTypeID'][value='" + bet + "']", ".room_" + roomtype).prop('checked', true);
            break;
        }
    }
});

$("[name$='HotelSpecialRequest.SmokingPreferences'], [name$='HotelSpecialRequest.CheckInMode']").on('change', function (event) {
    var prefixname = $(this).attr('name').split(']')[1];
    var prefixno = $(this).attr('name').split('[').pop().split(']').shift();
    var roomtype = $("option:selected", "select[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.RoomType']").index();
    var valchecked = $(this).val();
    var length = $('.booking-form').length - 1;
    for (i = 0; i < length; i++) {
        if ($("option:selected", "select[name='TravellerDetails[" + i + "].HotelSpecialRequest.RoomType']").index() == roomtype) {
            $("[name$='[" + i + "]" + prefixname + "'][value='" + valchecked + "']").prop('checked', true);
        }
    }
});

$("[name$='HotelSpecialRequest.BetTypeID']").on('change', function (event) {
    var prefixname = $(this).attr('name').split(']')[1];
    var prefixno = $(this).attr('name').split('[').pop().split(']').shift();
    var roomtype = $("option:selected", "select[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.RoomType']").index();
    var valchecked = $(this).val();
    var length = $('.booking-form').length - 1;
    for (i = 0; i < length; i++) {
        if ($("option:selected", "select[name='TravellerDetails[" + i + "].HotelSpecialRequest.RoomType']").index() == roomtype) {
            $("[name$='[" + i + "]" + prefixname + "'][value='" + valchecked + "']", ".room_" + roomtype).prop('checked', true);
        }
    }
});

$("[name$='HotelSpecialRequest.AdditionalRequest']").on('change', function (event) {
    var prefixno = $(this).attr('name').split('[').pop().split(']').shift();
    var roomtype = $("option:selected", "select[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.RoomType']").index();
    var text = $(this).val();
    var length = $('.booking-form').length - 1;
    for (i = 0; i < length; i++) {
        if ($("option:selected", "select[name='TravellerDetails[" + i + "].HotelSpecialRequest.RoomType']").index() == roomtype) {
            $("[name='TravellerDetails[" + i + "].HotelSpecialRequest.AdditionalRequest']").val(text);
        }
    }
});

$("[name^='TravellerDetails'][name$='DOB']").on('change', function (event) {
    var prefixno = $(this).attr('name').split('[').pop().split(']').shift();
    var age = $("[name^='TravellerDetails'][name$='Age']").eq(prefixno).val();
    if (age < 12) {
        $("[name='TravellerDetails[" + prefixno + "].HotelSpecialRequest.ImStaying']").prop("checked", false);
        $("#hotelSR_" + prefixno).hide();
    } else {
        $("#hotelSR_" + prefixno).show();
    }
});

//Bind selected baggage to consecutive segment
//$('.Baggage').on('change', function () {
//    var name = $(this).attr('name');
//    var IsOutBound = name.indexOf("OutBound") !== -1;
//    var prefixno = $(this).attr('name').split('[').pop().split(']').shift();
//    var outInBoundSSR = IsOutBound ? "OutBoundSSR" : "InBoundSSR";
//    var baggageValue = $(this).val();

//    var baggageOptions = $("select[name*='TravellerDetails[" + prefixno + "]." + outInBoundSSR + "'][class*='Baggage']");

//    $(baggageOptions).each(function (i, e) {
//        $(e).val(baggageValue);
//    });
//});