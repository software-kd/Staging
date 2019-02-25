$(document).ready(function () {
    //$.data($('form')[0], 'validator').settings.ignore = "";
    //$("#TourPackageForm").data("validator").settings.ignore = "";
    $("#TourPackageForm").validate().settings.ignore = '';

    $.fn.datepicker.defaults.autoclose = true;
    $.fn.datepicker.defaults.format = 'dd/mm/yyyy';
    $.fn.datepicker.defaults.startDate = '+15d';

    //$("#TourPackagesInfo_TravelDateFrom").datepicker();
    $('#TourPackagesInfo_TravelDateFrom').click(function () {
        $("#TourPackagesInfo_TravelDateFrom").datepicker('show');
    });

    $("#TourPackagesInfo_DepartFlightDetails_DepartureDatetime").datepicker({}).on('changeDate', function (selected) {
        var selectedBeginDate = $('#TourPackagesInfo_DepartFlightDetails_DepartureDatetime').datepicker('getDate');
        var selectedEndDate = $('#TourPackagesInfo_DepartFlightDetails_ArrivalDatetime').datepicker('getDate');
        $('#TourPackagesInfo_DepartFlightDetails_ArrivalDatetime').datepicker("setStartDate", selectedBeginDate);

        if (selectedBeginDate > selectedEndDate - 1) {
            $('#TourPackagesInfo_DepartFlightDetails_ArrivalDatetime').datepicker("setDate", selectedBeginDate);
        }

        $('#TourPackagesInfo_DepartFlightDetails_ArrivalDatetime').datepicker('show');
    });

    var getFltArrivalDate = $('#TourPackagesInfo_DepartFlightDetails_DepartureDatetime').datepicker('getDate') == null ? new Date() : $('#TourPackagesInfo_DepartFlightDetails_DepartureDatetime').datepicker('getDate');

    $("#TourPackagesInfo_DepartFlightDetails_ArrivalDatetime").datepicker({
        startDate: getFltArrivalDate,
    });

    $("#TourPackagesInfo_ArriveFlightDetails_DepartureDatetime").datepicker({}).on('changeDate', function (selected) {
        var selectedBeginDate = $('#TourPackagesInfo_ArriveFlightDetails_DepartureDatetime').datepicker('getDate');
        var selectedEndDate = $('#TourPackagesInfo_ArriveFlightDetails_ArrivalDatetime').datepicker('getDate');
        $('#TourPackagesInfo_ArriveFlightDetails_ArrivalDatetime').datepicker("setStartDate", selectedBeginDate);

        if (selectedBeginDate > selectedEndDate - 1) {
            $('#TourPackagesInfo_ArriveFlightDetails_ArrivalDatetime').datepicker("setDate", selectedBeginDate);
        }

        $('#TourPackagesInfo_ArriveFlightDetails_ArrivalDatetime').datepicker('show');
    });

    var getFlt2ArrivalDate = $('#TourPackagesInfo_ArriveFlightDetails_DepartureDatetime').datepicker('getDate') == null ? new Date() : $('#TourPackagesInfo_ArriveFlightDetails_DepartureDatetime').datepicker('getDate');

    $("#TourPackagesInfo_ArriveFlightDetails_ArrivalDatetime").datepicker({
        startDate: getFlt2ArrivalDate,
    });
});

$('.selectopt li').click(function () {
    $(this).find("input[type=radio]").prop("checked", true);
    $(this).parent().find('.hli').toggleClass('hli');
    $(this).toggleClass('hli');
    $(this).find("input[type=radio]").trigger('change');
});

$('.hotelselectopt li').click(function () {
    $(this).find("input[type=radio]").prop("checked", true);
    $(this).parents('#collapseOne').find('.hli').toggleClass('hli');
    $(this).toggleClass('hli');
    $("input[type=radio][name='TourPackagesInfo.HotelID']").trigger('change');
    refreshRoomType();
});

$(".collapse").on("shown.bs.collapse", function () {
    $(this).parents('.left-filter').removeClass('red-border-bottom');
});
$('.collapse').on('hidden.bs.collapse', function () {
    $(this).parents('.left-filter').toggleClass('red-border-bottom');
})

$('#btnPageSubmit').on('click', function (e) {
    if ($("#TourPackageForm").valid()) {
        $("#TourPackageForm").submit();
    } else {
        checkMarker();
    }
});
$('.tour-input').on('click', '.tour-traveler', function () {
    if ($(this).hasClass("d-block")) {
        $(this).removeClass("d-block");
    } else {
        $(this).addClass("d-block");
    }
});

var checkMarker = function () {
    var errList = $('#TourPackageForm').validate().errorList;

    var errForm = [];
    for (var i = 0; i < errList.length; i++) {
        var errElement = errList[i].element;
        var formId = $('.collapse').has(errElement).attr('id');
        $("#" + formId).collapse('show');
    }
}

$('.tour-input').on('change', "input[name$='TravelDateFrom']", function () {
    if ($(this).val() != "") {
        $(".reservation-table-top #checkin").html($("input[name$='TravelDateFrom']").val());
    } else {
        $(".reservation-table-top #checkin").html("dd-MMM-yyyy");
    }
});
$('.tour-input').on('change', "input[name$='TravelDateTo']", function () {
    if ($(this).val() != "") {
        $(".reservation-table-top #checkout").html($("input[name$='TravelDateTo']").val());
    } else {
        $(".reservation-table-top #checkout").html("dd-MMM-yyyy");
    }
});

$("#enquiry").click(function () {
    if ($("#TourPackageForm").valid()) {
        var content = "<textarea rows='4' cols='50' name='enquiryQuestion' form='TourPackageForm'></textarea>";
        dynamicModal('Please enter your enquiry question', content, true).modal();
    } else {
        checkMarker();
    }
});
$(".btn.modal-confirm").click(function () {
    if ($("#TourPackageForm").valid()) {
        var data = $("#TourPackageForm").serialize();
        $.ajax({
            url: '/TourPackage/TourPackageEnquiry',
            type: 'POST',
            data: data,
            cache: false,
            async: true,
            beforeSend: function () {
                loadModal(true);
            },
        }).done(function (res) {
            loadModal(false);
            if(res == 'True'){
                dynamicModal('', 'We have received your enquiry and will be contacting you soon. Thank you!', false).modal();
            } else {
                dynamicModal('', 'There is failed to send your email. Please try again.', false).modal();
            }
        });
    } else {
        checkMarker();
    }
});

$("#TourPackageForm").on('change', '#TourPackagesInfo_ExtensionNight', function () {
    var extension = $(this).val();
    $(".reservation-extension").html(extension);
});

$("#TourPackageForm").on('change', '#TourPackagesInfo_NoOfPax', function () {
    var extension = $(this).val();
    $(".reservation-pax").html(extension);
});

$("#TourPackageForm").on('click', '.tourbookbtn', function () {
    var hotelID = $(this).attr("data-hotel");
    var minToGo = $(this).attr("data-min");
    if ($(this).hasClass("confirmbtn")) {
        var qtyinput = $("select[data-hotel='" + hotelID + "'][data-min='" + minToGo + "']");
        if ($(this).html() == "Edit") {
            qtyinput.prop('disabled', false);
            $(this).html("Confirm");
        } else {
            var etcP = [];
            var _qty = 0;
            qtyinput.each(function (i, e) {
                var pEle = $(e);
                var v = pEle.val();
                if (v > 0) {
                    _qty += parseInt(v);
                    etcP.push({
                        hotelID: pEle.data('hotel'),
                        RoomTypeID: pEle.data('roomid'),
                        RoomPriceID: pEle.data('priceid'),
                        Qty: v,
                    });
                }
            });
            if (_qty > 0) {
                $("[name='travelerdata']").val(JSON.stringify(etcP));
            } else {
                $("[name='travelerdata']").val("");
            }
            var etcP = $("[name='travelerdata']").val();
            if (etcP.length > 0) {
                if ($("[name='TtlPaxSelected']").val() != $("#TourPackagesInfo_NoOfPax").val()) {
                    var content = '<div class="text-center">Room quantity does not match number of travellers. Please re-select again<br/></div>';
                    dynamicModal('', content, false).modal();
                } else {
                    $("#btnPageSubmit").removeAttr('disabled');
                    qtyinput.prop('disabled', true);
                    $(this).html("Edit");
                }
                //$("#TourPackageForm").submit();
            } else {
                var content = '<div class="text-center">Please select tour room type.<br/></div>';
                dynamicModal('', content, false).modal();
            }
        }
    } else {
        $(".tourbookqty").hide();
        $(".tourbookqty input").val("");
        $(".tourbookbtn").removeClass("confirmbtn");
        $(".tourbookbtn").html("Select");
        $(this).addClass("confirmbtn");
        $(this).html("Confirm");
        $("#tourhoteltxt").html($(this).attr("data-hname"));
        var qtycol = $(".tourbookqty[data-hotel='" + hotelID + "'][data-min='" + minToGo + "']");
        qtycol.show();
        //qtycol.siblings(".tourbookqty").show();
        refreshRoomType(hotelID, minToGo);
    }
});

var calcTour = function (room, trans) {
    return $.ajax({
        url: tourCalcUrl + location.search,
        type: 'POST',
        data: {
            Room: room,
            Transport:trans
        },
        async: true,
        dataType: 'json',
        beforeSend: function () {
            loadModal(true);
        },
    }).promise();
}

var calcTourAddon = function (trans, ent, lang) {
    return $.ajax({
        url: tourCalcUrl + location.search,
        type: 'POST',
        data: {
            transportID: (typeof trans === 'undefined') ? 0 : trans,
            entranceID: (typeof ent === 'undefined') ? 0 : ent,
            languageID: (typeof lang === 'undefined') ? 0 : lang
        },
        async: true,
        dataType: 'json',
        beforeSend: function () {
            loadModal(true);
        },
    }).promise();
}

$("#TourPackageForm").on('change', 'select', function () {
    var ele = $(".confirmbtn");
    var hotelID = ele.attr("data-hotel");
    var minToGo = ele.attr("data-min");
    var qtyinput = $("select[data-hotel='" + hotelID + "'][data-min='" + minToGo + "']");
    var etcP = [];
    var _qty = 0;
    qtyinput.each(function (i, e) {
        var pEle = $(e);
        var v = pEle.val();
        if (v > 0) {
            _qty += parseInt(v);
            etcP.push({
                hotelID: pEle.data('hotel'),
                RoomTypeID: pEle.data('roomid'),
                RoomPriceID: pEle.data('priceid'),
                Qty: v,
            });
        }
    });
    if (_qty > 0) {
        $("[name='travelerdata']").val(JSON.stringify(etcP));
    } else {
        $("[name='travelerdata']").val("");
    }
    var room = $("#travelerdata").val();
    var trans = $("#TourPackagesInfo_TransportPackageID:checked").val();
    calcTour(room, trans).then(function (obj) {
        if (typeof obj === 'string') {
            $('body').append(obj);
        } else {
            $(".reservation-room").remove();
            $(".reservation-table-mid ul").prepend(obj.roomdesc);
            $('.reservation-total').html("MYR " + obj.ttl);
            $('.reservation-ttlperpax').html("MYR " + obj.ttlper);
            $("[name='TtlPaxSelected']").val(obj.ttlpax);

            qtyinput.not(ele).each(function (index, element) {
                $(element).find("option").show();
                var op = (10 - $("[name='TtlPaxSelected']").val()) / $(element).data('pax');
                var option = parseInt($(element).val()) + op;
                for (x = 10; x > option; x--) {
                    $(element).find("option[value=" + x + "]").hide();
                }
            });
            loadModal(false);
        }
    }, function (xhr, status, error) {
        dynamicModal('Error', xhr.responseText, false).modal();
    });
});

$("#TourPackageForm").on('change', '#TourPackagesInfo_TransportPackageID, #TourPackagesInfo_EntranceID, #TourPackagesInfo_LanguageID', function () {
    var trans = $("#TourPackagesInfo_TransportPackageID:checked").val() ;
    var ent = $("#TourPackagesInfo_EntranceID:checked").val();
    var lang = $("#TourPackagesInfo_LanguageID:checked").val();
    calcTourAddon(trans, ent, lang).then(function (obj) {
        if (typeof obj === 'string') {
            $('body').append(obj);
        } else {
            $(".reservation-room.tpaddon").remove();
            $(".reservation-table-mid ul").append(obj.roomdesc);
            $('.reservation-total').html("MYR " + obj.ttl);
            $('.reservation-ttlperpax').html("MYR " + obj.ttlper);
            $("[name='TtlPaxSelected']").val(obj.ttlpax);
            loadModal(false);
        }
    }, function (xhr, status, error) {
        dynamicModal('Error', xhr.responseText, false).modal();
    });
});

function refreshRoomType(hotelID, minToGo) {
    var paxno = $("#TourPackagesInfo_NoOfPax").val();
    var singleroompax = 0;
    if (paxno > 0 && paxno <= 10) {
        if (paxno % 2 == 1) {
            var room1pax = $("select[data-hotel='" + hotelID + "'][data-min='" + minToGo + "'][data-pax='1']:first");
            if (room1pax.length > 0) {
                room1pax.val(1);
                room1pax.trigger('change');
            } else {
                singleroompax = 1;
            }
            paxno = paxno - 1;
        }
        if (paxno > 0) {
            var roomno = (paxno / 2) + singleroompax;
            var room2pax = $("select[data-hotel='" + hotelID + "'][data-min='" + minToGo + "'][data-pax='2']:first");
            room2pax.val(roomno);
            room2pax.trigger('change');
        }
    }
}

function getModelName(fieldName) {
    return fieldName.substr(fieldName.lastIndexOf(".") + 1, fieldName.length);
}

$(function () {
    $(document).ready(function (e) {
        $.validator.methods.date = function (value, element, params) {
            var isValid = value != '' ? moment(value, 'DD/MM/YYYY', true).isValid() : true;
            var name = typeof element.name != 'undefined' ? element.name : '';
            var modelName = getModelName(element.name);

            switch (modelName) {
                case "TravelDateFrom":
                    var selectDOB = $("input[name$='TravelDateFrom']").map(function (i, e) {
                        return $(e).val();
                    }).get();
                    var isValidDOB = true;
                    isValid = isValid && isValidDOB;
                    break;
                default:
                    break;
            }

            return isValid;
        }
    });
    $("input[name$='TravelDateFrom']").rules('add', 'date');
});