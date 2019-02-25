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
        if ($('#RoomDetails_' + i + '__GuestDOB').val() == $('#ContactPerson_DOB').val()) {
            $('#RoomDetails_' + i + '__GuestDOB').val('');
        }
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
    $('#RoomDetails_' + index + '__GuestDOB').val($('#ContactPerson_DOB').val());
}
var bindGuestInfo = function () {
    var length = $('#roomChoise').children('option').length;
    for (i = 1; i < length; i++) {
        $('#RoomDetails_' + i + '__Title').prop('selectedIndex', $('#RoomDetails_0__Title').prop('selectedIndex'));
        $('#RoomDetails_' + i + '__GivenName').val($('#RoomDetails_0__GivenName').val());
        $('#RoomDetails_' + i + '__Surname').val($('#RoomDetails_0__Surname').val());
        $('#RoomDetails_' + i + '__GuestDOB').val($('#RoomDetails_0__GuestDOB').val());
        $('#RoomDetails_' + i + '__SpecialRequest_AdditionalRequest').val($('#RoomDetails_0__SpecialRequest_AdditionalRequest').val());
        $('#RoomDetails_' + i + '__GivenName').blur();
    }
}
var clearAllApply = function () {
    var length = $('#roomChoise').children('option').length;
    for (i = 1; i < length; i++) {
        $('#RoomDetails_' + i + '__Title').prop('selectedIndex', $('#RoomDetails_0__Title').prop('selectedIndex'));
        $('#RoomDetails_' + i + '__GivenName').val('');
        $('#RoomDetails_' + i + '__Surname').val('');
        $('#RoomDetails_' + i + '__GuestDOB').val('');
        $('#RoomDetails_' + i + '__SpecialRequest_AdditionalRequest').val('');
        $('#RoomDetails_' + i + '__GivenName').blur();
    }
}
//Hotel Function - END

//Flight Function - START
var contactPersonIdList = ['#ContactPerson_GivenName', '#ContactPerson_Surname', '#ContactPerson_Email', '#ContactPerson_Phone1'];
var contactPersonId = '#ContactPerson_GivenName, #ContactPerson_Surname, #ContactPerson_Email, #ContactPerson_Phone1, #ContactPerson_DOB, #ContactPerson_CountryCode,#ContactPerson_Title';
var isGATE = typeof FltSup != "undefined" && (FltSup == "GATE_Int" || FltSup == "GATE_Chn");
var isAirAsia = typeof FltSup != "undefined" && FltSup == "AirAsia";

var bindContactToGuest = function () {
    $('#TravellerDetails_0__GivenName').val($('#ContactPerson_GivenName').val());
    $('#TravellerDetails_0__Surname').val($('#ContactPerson_Surname').val());
    $('#TravellerDetails_0__Title').val($('#ContactPerson_Title').val());
    $('#TravellerDetails_0__Title').prop('selectedIndex', $('#ContactPerson_Title').prop('selectedIndex'));
    $('#TravellerDetails_0__DOB').val($('#ContactPerson_DOB').val());
    if ($('#ContactPerson_DOB').val() != '') {
        $("#TravellerDetails_0__DOB").data('daterangepicker').setStartDate(moment($('#ContactPerson_DOB').val(), 'DD-MMM-YYYY'));
    }
    //$('#TravellerDetails_0__DOB').datepicker().datepicker('setDate', $('#ContactPerson_DOB').val());
    $('#TravellerDetails_0__Nationality').val($('#ContactPerson_Nationality').val());
    $('#TravellerDetails_0__Age').val($('#ContactPerson_Age').val());
    $("#TravellerDetails_0__Nationality").trigger('change');
    $("#TravellerDetails_0__PassportNumber").val($('#PassportNo').val());
    $("#TravellerDetails_0__PassportExpiryDate").val($('#PassportExpdate').val());
    $("#TravellerDetails_0__PassportExpiryDate").trigger('change');
    $("#TravellerDetails_0__PassportIssueCountry").val($('#PassportCountry').val());
    $('#TravellerDetails_0__DOB').trigger('change');
}
var clearFltGuest1 = function () {
    $('#TravellerDetails_0__GivenName').val('');
    $('#TravellerDetails_0__Surname').val('');
    $('#TravellerDetails_0__Title').val($("#TravellerDetails_0__Title option:first").val());
    $('#TravellerDetails_0__Nationality').val('MYS');
    $("#TravellerDetails_0__DOB").val('');
    $('#TravellerDetails_0__Age').val('');
    $("#TravellerDetails_0__PassportNumber").val('');
    $("#TravellerDetails_0__PassportExpiryDate").val('');
    $("#TravellerDetails_0__PassportIssueCountry").val('');
}
var passportValidation = function () {
    var msg = [];
    $('input[name$="PassportExpiryDate"]').each(function (index, element) {
        var passportDate = $(element).val();
        var prefix = getModelPrefix(element.name);
        if (passportDate.length) {
            var tripDate = typeof ServerDateTime == 'undefined' ? $('#tripBeginDate').val() : ServerDateTime;
            if (moment(passportDate, 'DD-MMM-YYYY', true) <= moment(tripDate, 'DD-MMM-YYYY', true).add(6, 'M')) {
                var pName = $('input[name="' + prefix + 'GivenName"').val() + ' ' + $('input[name="' + prefix + 'Surname"').val();
                var parsePassDate = moment(passportDate, 'DD-MMM-YYYY', true).format('DD-MMM-YYYY');
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

            if (!moment(value, 'YYYY-MM-DD').isValid()) {
                value = moment(value, "DD-MMM-YYYY").format("YYYY-MM-DD");
            }

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

            return moment(passportDate, 'DD-MMM-YYYY', true) >= moment(tripDate, 'DD-MMM-YYYY', true);
        }
            , function (value, element, params) {
                var tripDate = typeof ServerDateTime == 'undefined' ? $('#tripBeginDate').val() : ServerDateTime;
                var parseDate = moment(tripDate, 'DD-MMM-YYYY', true).format("DD-MMM-YYYY");
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
            var isValid = value != '' ? moment(value, 'DD-MMM-YYYY', true).isValid() : true;
            var name = typeof element.name != 'undefined' ? element.name : '';
            var prefix = getModelPrefix(name);
            var modelName = getModelName(element.name);

            switch (modelName) {
                case "DateOfBirth":
                    var selectDOB = $('select[name*="' + prefix + 'DateOfBirth"]').map(function (i, e) {
                        return $(e).val();
                    }).get();
                    var isValidDOB = true;
                    isValid = isValid && isValidDOB;
                    break;
                case "DOB":
                    var selectDOB = $('select[name*="' + prefix + 'DOB"]').map(function (i, e) {
                        return $(e).val();
                    }).get();
                    var isValidDOB = true;
                    isValid = isValid && isValidDOB;
                    break;
                case "PassportExpiryDate":
                    var selectPassport = $('select[name*="' + prefix + 'PassportExpiryDate"]').map(function (i, e) {
                        return $(e).val();
                    }).get();
                    var isValidPassport = true;
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

        //$("#registerCheckBox").prop('checked', true);

        if ($('#registerCheckBox').prop('checked')) {
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

        $.checkRegister($('#ContactPerson_Email').val());

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
                var nowForm = $('a[href="#' + formId + '"]');

                if (isElementExists) {
                    nowForm.tab('show');
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

    var toggleOptionalRegister = function (display) {
        var chkBox = $('#registerCheckBox');
        if (display) {
            $(".s3_optional_register").show();
        }
        else {
            $("#RegistedLoginModal").modal('show');

            $('#registerCheckBox').prop('checked', false);
            $(".s3_optional_register").hide();
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

    var checkMarker = function (validNow) {
        validNow = typeof validNow == "undefined" ? true : validNow;
        if (validNow) {
            //toggleSubmitBtn();
        }
        var errList = formValidator.errorList;

        var errForm = [];
        for (var i = 0; i < errList.length; i++) {
            var errElement = errList[i].element;

            var formId = $('.booking-form').has(errElement).attr('id');
            if ($.inArray(formId, errForm) == -1) {
                errForm.push(formId);
            }
        }
    }
    var checkOneFormAllValid = function (prefix, bookingFormId) {
        //toggleSubmitBtn();
        if (prefix.indexOf("SpecialRequest")) {
            prefix = prefix.replace("SpecialRequest.", "")
        }

        if (prefix.length) {
            var isValid = $('input[name*="' + prefix + '"]').valid();
        }
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
    $(document).on('blur', contactPersonId, function (event) {
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
            toggleOptionalRegister(res);
        });
    };

    // preparation for valid without submit
    $("#btnSubmitForm").on('click', function (e) {
        if (validateForm()) {
            var msg = passportValidation();
            if (msg.length && !$('#popup-modal').is(':visible')) {
                var content = "<div>Generally all travellers&apos; passports must have the min. validity of 6 months from the departure date. Therefore, please be reminded to renew your passport before your trip. You may check with your local immigration body for more details.<br/><br/>Guest require attention:</div>";
                $(msg).each(function (i, value) {
                    content += "<div class='default-red'>" + value + "</div>";
                });
                $(".btn.modal-confirm").text("Continue");
                $(".btn.modal-close").text("Cancel");
                dynamicModal('Notice', content, true).modal();
            }
            else {
                form.submit();
                if (typeof sessionStorage.ecomselected == 'undefined') {
                    trackCheckout2(GTM_trackAddToCart);
                }
                if (typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}') {
                    ecomcheckout(2);
                }
                $('button#btnSubmitForm').attr('disabled', 'disabled');
                $('button#btnSubmitForm').css('background-color', '#b1a9a9 !important', 'cursor', 'default');
                $('button#btnSubmitForm').text('Please wait...');
                $('form .btn-loadstate').show();
            }
        }
    });

    $(document).on('blur','input', function (e) {
        var name = $(this).attr('name');
        var prefix = getModelPrefix(name);
        var bookingFormId = $('.booking-form').has(this).attr('id');
        checkOneFormAllValid(prefix, bookingFormId);
    });

    $('#checkoutDetailForm').on('change', 'input[type="hidden"]', function (e) {
        var prefix = getModelPrefix($(this).attr('name'));
        var bookingFormId = $('.booking-form').has(this).attr('id');
        checkOneFormAllValid(prefix, bookingFormId);
    });
    $(document).on('blur', '#ContactPerson_Email', function (event) {
        $.checkRegister(this.value);
    });
    $("#checkbox3, #checkbox2").on('change', function (event) {
        var imFlying = $(this).is(':checked');
        var dfd = $.Deferred();
        if (imFlying) {
            bindContactToGuest();
            dfd.resolve();
        } else {
            var content = "<div>Guest Information will be REMOVE<br/>Continue proceed?</div>";
            $(".btn.modal-confirm").text("Yes");
            $(".btn.modal-close").text("Cancel");
            dynamicModal('Confirm', content, true).modal();
            $(this).prop('checked', true);
        }dfd.done(function (e) {
            checkMarker();
        });
    })
    $('#genericModal').on('click', '.modal-confirm', function (e) {
        if ($("#genericModalLabel").text() == "Notice") {
            form.submit();
            if (typeof sessionStorage.ecomselected == 'undefined') {
                trackCheckout2(GTM_trackAddToCart);
            }
            if (typeof sessionStorage.ecomselected !== 'undefined' && sessionStorage.ecomselected !== '' && sessionStorage.ecomselected !== '{}') {
                ecomcheckout(2);
            }
            $('button#btnSubmitForm').attr('disabled', 'disabled');
            $('button#btnSubmitForm').css('background-color', '#b1a9a9 !important', 'cursor', 'default');
            $('button#btnSubmitForm').text('Please wait...');
            $('form .btn-loadstate').show();
        } else {
            var IsFlightBooking = $("[name$=PassengerType]").length > 0;
            if (!IsFlightBooking) {
                clearGuest1();
                $("#checkbox3").prop('checked', false);
            } else {
                clearFltGuest1();
                $("#checkbox2").prop('checked', false);
            }
            checkMarker();
            $('#genericModal').modal('hide');
        }
    });

    $('#registerCheckBox').on('change', function () {
        if ($(this).prop('checked')) {
            $("[name$=Password]").rules("add", "required");
            $("#MemberRegisterModels_AgreeTnC").rules("add", "agreetnc");
            $('.pw-wrap').fadeIn(100);
        } else {
            $("[name$=Password]").rules("remove", "required");
            $("#MemberRegisterModels_AgreeTnC").rules("remove", "agreetnc");
            $("#MemberRegisterModels_Password").val('');
            $("#MemberRegisterModels_ConfirmPassword").val('');
            $('.pw-wrap').fadeOut(100);
        }
    });


    $("#MemberRegisterModels_AgreeTnC").on('change', function (e) {
        $(this).valid();
    })
    $("input#registerCheckBox").on('focus', function (event) {
        $("input[type=checkbox]:not(old) + input + label > span").addClass("focusborder");
    });
    $(document).on('blur', 'input#registerCheckBox', function (event) {
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
$(document).on("blur", '#ContactPerson_Title, #ContactPerson_GivenName, #ContactPerson_Surname, #ContactPerson_DOB', function (event) {
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
            $('#RoomDetails_' + i + '__GuestDOB').change();
        }
    }
});

$("[name$='GuestDOB']").change(function () {
    var hotelsup = $("#Supplier").val();
    var GuestDOBInput = $("input[name*='RoomDetails'][name$='GuestDOB']");
    var prefix = $(this).attr('name');
    var prefixno = prefix.split('[').pop().split(']').shift();
    GuestDOBInput.eq(prefixno).rules('remove', 'required');
    if (hotelsup == "HotelBeds") {
        GuestDOBInput.eq(prefixno).rules('add', {
            required: true,
            messages: {
                required: "DOB is required",
            }
        });
    }
});

$(document).on('blur', '[name^="RoomDetails"][name$="GivenName"], [name^="RoomDetails"][name$="Surname"]', function () {
    var hotelsup = $("#Supplier").val();
    if (hotelsup == "Tourplan") {
        for (var x = 1; x < $('.booking-form').length - 1; x++) {
            $('#RoomDetails_' + x + '__Surname').rules('add', 'checkSameName');
        }
    }
});

$(document).ready(function () {
    if ($("#Supplier").val() == "HotelBeds" && $("[name$=PassengerType]").length > 0) {
        $("select[name='ContactPerson.DOB']").change();
    }
});

$("[name='ContactPerson.DOB']").change(function () {
    var ContactDOBInput = $("input[name='ContactPerson.DOB']");
    ContactDOBInput.rules('remove', 'required');
    if ($("#Supplier").val() == "HotelBeds" && $("[name$=PassengerType]").length > 0) {
        ContactDOBInput.rules('add', {
            required: true,
            messages: {
                required: "DOB is required",
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

$("[name^='TravellerDetails'][name$='DOB']").on('change', function (e) {
    var hInputName = $(this).attr('name');
    var prefix = getModelPrefix(hInputName);

    // get age here and assign.
    if (!moment($(this).val(), 'YYYY-MM-DD').isValid()) {
        dateField = new Date(moment($(this).val(), "DD-MMM-YYYY").format("YYYY-MM-DD"));
    }
    else {
        dateField = new Date($(this).val());
    }
    
    if (hInputName.search('DOB') != -1) {
        var passengerType = $('input:hidden[name="' + prefix + 'PassengerType"]').val();
        var today = ServerEndDateTime;
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

    // valid date hidden input field 
    $('input[name*="' + prefix + 'DOB"]').valid();
    if ($('input[name*="' + prefix + 'PassportExpiryDate"]').length) {
        $('input[name*="' + prefix + 'PassportExpiryDate"]').valid();
    }
});

//ZK on Frequent Traveller PopOut start
//for popout _FrequentFlyerPopOut.cshtml
$('.s3-1_flyer_left_dupinput').click(function () {
    var Filter = $("#filterforffair").val();
    var ffAir = $(this).data("ffair");
    var AirlineType = $(this).data("airlinetype");

    var GuestNotest = ffAir.split("_").pop();
    $('#loadingModal').show();
    $.ajax({
        type: "GET",
        url: '/Checkout/GetFrequentFlyerPopOut?',
        data: {
            filter: Filter,
            airlinetype: AirlineType
        },
        cache: false,
        async: false,
        dataType: "text",
        beforeSend: function () {
            //$('#loading-modal').show();
        },
    }).done(function (result) {
        unloadScrollBars();
        setTimeout(function () {
            var containSection = $(result).find('#listContainer').html();
            $("#ffleyer_popModal").modal('show');
            if (AirlineType == "AK") {
                $("#filterforffair").attr('disabled', true);
            }
            $('#loadingModal').hide();
            $('#AirLineList').html(containSection == "" ? result : "<div id='listContainer'>" + containSection + "</div>");
            $("#guestnoforffair").val(GuestNotest);

        }, 0);
    });
});

$('#ffleyer_pop').on('click', '.frequentFlyerClick', function () {
    $("#ffleyer_popModal").modal('hide');
    var flightId = $(this).data("id");
    var logoUrlLink = $(this).data("logourl");
    var guestNo = $("#guestnoforffair").val();

    var textUrl = "<img src='" + logoUrlLink + "' style='height:30px;width:30px;'/><span> " + flightId + "</span>";
    var appendImgSpanID = "#ffairID_" + guestNo;
    var putNoID = "#TravellerDetails_" + guestNo + "__FrequrntFlyerNoAirline";
    var frequentFlyerNo = "#TravellerDetails_" + guestNo + "__FrequentFlyerNo"
    var errorMessageID = "#FFError_" + guestNo;

    $(appendImgSpanID).find("img").remove();
    $(appendImgSpanID).find("span").remove();
    $(appendImgSpanID).append(textUrl);
    $(putNoID).val(flightId);

    $(frequentFlyerNo).attr('disabled', false);

    if ($('#TravellerDetails_' + guestNo + '__FrequrntFlyerNoAirline').val() != "") {
        $(frequentFlyerNo).rules('add', {
            required: true,
            messages: {
                required: "Please fill in Frequent Traveller No.",
            }
        });
    }
    else {
        $(frequentFlyerNo).rules('remove', 'required');
    }
    reloadScrollBars();
});
