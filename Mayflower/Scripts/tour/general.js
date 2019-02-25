//Count unit
$('.count-up').on('click', function (e) {
    e.preventDefault();
    var getCurrentValue = $(this).parent('.tour-cuntom-unit-inner').find('span').html();

    if (getCurrentValue >= 0) {
        getCurrentValue++;

        $(this).parent('.tour-cuntom-unit-inner').find('span').html(getCurrentValue);
        $(this).parent('.tour-cuntom-unit-inner').parent('.custom-unit').find('input').val(getCurrentValue); //update input value
    } else {
        $(this).parent('.tour-cuntom-unit-inner').find('span').html('0');
        $(this).parent('.tour-cuntom-unit-inner').parent('.custom-unit').find('input').val(0);
    }
    $(this).parent('.tour-cuntom-unit-inner').parent('.custom-unit').find('input').trigger('change');

});
$('.count-down').on('click', function (e) {
    e.preventDefault();
    var getCurrentValue = $(this).parent('.tour-cuntom-unit-inner').find('span').html();

    if (getCurrentValue > 0) {
        getCurrentValue--;

        $(this).parent('.tour-cuntom-unit-inner').find('span').html(getCurrentValue);
        $(this).parent('.tour-cuntom-unit-inner').parent('.custom-unit').find('input').val(getCurrentValue);
    } else {
        $(this).parent('.tour-cuntom-unit-inner').find('span').html('0');
        $(this).parent('.tour-cuntom-unit-inner').parent('.custom-unit').find('input').val(0);
    }
    $(this).parent('.tour-cuntom-unit-inner').parent('.custom-unit').find('input').trigger('change');

});

$(document).ready(function () {
    //$.data($('form')[0], 'validator').settings.ignore = "";
    //$("#TourPackageForm").data("validator").settings.ignore = "";
    $("#TourPackageForm").validate().settings.ignore = '';

    $.fn.datepicker.defaults.autoclose = true;
    $.fn.datepicker.defaults.format = 'yyyy/mm/dd';
    $.fn.datepicker.defaults.startDate = '+15d';

    $("#TourPackagesInfo_TravelDateFrom").datepicker({}).on('changeDate', function (selected) {

        var selectedBeginDate = $('#TourPackagesInfo_TravelDateFrom').datepicker('getDate');
        var selectedEndDate = $('#TourPackagesInfo_TravelDateTo').datepicker('getDate');
        var default_start = new Date(selectedBeginDate.getFullYear(), selectedBeginDate.getMonth(), selectedBeginDate.getDate() + 1);
        var setStartDate = new Date(selectedBeginDate.getFullYear(), selectedBeginDate.getMonth(), selectedBeginDate.getDate() + 1);
        $('#TourPackagesInfo_TravelDateTo').datepicker("setStartDate", setStartDate);

        if (selectedBeginDate > selectedEndDate - 1) {
            $('#TourPackagesInfo_TravelDateTo').datepicker("setDate", default_start);
        }

        $('#TourPackagesInfo_TravelDateTo').datepicker('show');
    });

    var getArrivalDate = $('#TourPackagesInfo_TravelDateFrom').datepicker('getDate') == null ? new Date() : $('#TourPackagesInfo_TravelDateFrom').datepicker('getDate');
    var setForDepart = new Date(getArrivalDate.getFullYear(), getArrivalDate.getMonth(), getArrivalDate.getDate() + 1);

    $("#TourPackagesInfo_TravelDateTo").datepicker({
        startDate: setForDepart,
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
$('#prompt-btn').on('click', function (e) {
    if ($("#TourPackageForm").valid()) {
        var addCount = 0;
        var etcP = [];
        var list = [];
        $(".tour-traveler-select").each(function (i, e) {
            var pEle = $(e);
            var v = pEle.val();
            var _qty = 0;
            if (v > 0) {
                _qty += parseInt(v);
                etcP.push({
                    RoomTypeName: pEle.data('name'),
                    RoomTypeID: pEle.data('tick'),
                    Price: pEle.data('price'),
                    catname: pEle.data('catname'),
                    NoOfPax: pEle.data('pax'),
                    Qty: v,
                });
                var addon = {
                    name: pEle.data('name'),
                    id: $(e).data('tick'),
                    price: $(e).data('price'),
                    quantity: v
                };
                list.push(addon);
                addCount++;
                $("[name='travelerdata']").val(JSON.stringify(etcP));
            }
        });
        if (etcP.length > 0 || addCount > 0) {
            //trackSelectAddOn(list);
            $("#TourPackageForm").submit();
        } else {
            var content = '<div class="text-center">Please select tour room type.<br/></div>';
            dynamicModal('', content, false).modal();
        }
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

$('.tour-input').on('change', "input.tour-traveler-select", function () {
    var etcP = [];
    var displayhtml = "";
    $(".tour-traveler-select").each(function (i, e) {
        var pEle = $(e);
        var v = pEle.val();
        if (v > 0) {
            etcP.push({
                RoomTypeName: pEle.data('name'),
                RoomTypeID: pEle.data('tick'),
                Price: pEle.data('price'),
                catname: pEle.data('catname'),
                NoOfPax: pEle.data('pax'),
                Qty: v,
            });
            var roomprice = pEle.data('price') * v;
            displayhtml += "<li class='reservation-room'><span>" + pEle.data('name') + " x " + v + "</span>";
            displayhtml += "<span>MYR" + roomprice.toFixed(2) + "</span></li>";
        }
    });
    $("[name='travelerdata']").val(JSON.stringify(etcP));
    //$("[name='travelerdata']").trigger('change');
    $(".reservation-room").remove();
    $(".reservation-table-mid ul").prepend(displayhtml);
    updateTourPrice();
});
$('.tour-input').on('change', "#TourPackagesInfo_TransportPackageID, #TourPackagesInfo_LanguageID", function () {
    updateTourPrice();
});

var calcTour = function (room, la, en, tran, pax) {
    return $.ajax({
        url: tourCalcUrl + location.search,
        type: 'POST',
        data: {
            Room: room,
            LanguageID: la,
            EntranceID: en,
            TransportPackageID: tran,
            NoOfPax: pax,
        },
        async: true,
        dataType: 'json',
        beforeSend: function () {
            loadModal(true);
        },
    }).promise();
}

var ctrlTour = function (obj) {
    $('.reservation-total').html("MYR " + obj.ttl);
    $('.reservation-ttlperpax').html("MYR " + obj.ttlper);
    $('.reservation-pax').html(obj.NoPax);
}

$("#enquiry").click(function () {
    if ($("#TourPackageForm").valid()) {
        var _qty = 0;
        $(".tour-traveler-select").each(function (i, e) {
            var v = $(e).val();
            if (v > 0) {
                _qty += parseInt(v);
            }
        });
        if (_qty > 0) {
            var content = "<textarea rows='4' cols='50' name='enquiryQuestion' form='TourPackageForm'></textarea>";
            dynamicModal('Please enter your enquiry question', content, true).modal();
        } else {
            dynamicModal('', '<div class="text-center">Please select tour room type.<br/></div>', false).modal();
        }
    } else {
        checkMarker();
    }
});
$(".btn.modal-confirm").click(function () {
    if ($("#TourPackageForm").valid()) {
        var data = $("#TourPackageForm").serialize();
        $.ajax({
            url: '/Checkout/TourPackageEnquiry',
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

$("#TourPackagesInfo_NoOfPax").change(function () {
    refreshRoomType();
});
function updateTourPrice() {
    var room = $("#travelerdata").val();
    var la = $("[name='TourPackagesInfo.LanguageID']:checked").val();
    var en = $("[name='TourPackagesInfo.EntranceID']:checked").val();
    var tran = $("[name='TourPackagesInfo.TransportPackageID']:checked").val();
    var pax = $("[name='TourPackagesInfo.NoOfPax']").val();
    calcTour(room, la, en, tran, pax).then(function (obj) {
        if (typeof obj === 'string') {
            $('body').append(obj);
        } else {
            ctrlTour(obj);
            loadModal(false);
        }
    }, function (xhr, status, error) {
        dynamicModal('Error', xhr.responseText, false).modal();
    });
}
function refreshRoomType() {
    if ($("input[name='TourPackagesInfo.HotelID']").is(':checked')) {
        $(".tour-cuntom-unit-inner span.col").html("0");
        $(".tour-traveler-select").val(0);
        $(".tour-select-msg").hide();

        var paxno = $("#TourPackagesInfo_NoOfPax").val();
        var hotelid = $("input[name='TourPackagesInfo.HotelID']:checked").val();
        var roomforhotelselected = $(".tour-room[data-hotel='" + hotelid + "']");
        var singleroompax = 0;
        $(".tour-room").not("[data-hotel='" + hotelid + "']").hide();
        roomforhotelselected.show();

        if (paxno > 0 && paxno <= 10) {
            if (paxno % 2 == 1) {
                var room1pax = roomforhotelselected.find(".tour-traveler-select[data-pax='1']:first");
                if (room1pax.length > 0) {
                    room1pax.parent().find(".tour-cuntom-unit-inner span").html(1);
                    room1pax.val(1);
                    room1pax.trigger('change');
                } else {
                    singleroompax = 1;
                }
                paxno = paxno - 1;
            }
            if (paxno > 0) {
                var roomno = (paxno / 2) + singleroompax;
                var room2pax = roomforhotelselected.find(".tour-traveler-select[data-pax='2']:first");
                room2pax.parent().find(".tour-cuntom-unit-inner span").html(roomno);
                room2pax.val(roomno);
                room2pax.trigger('change');
            }
        }
    }
}