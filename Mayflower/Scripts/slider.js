$.getFilterDetail = function () {
    return $.ajax({
        url: '/Flight/GetFullFlightSearchResultDetail',
        async: true, dataType: 'json', method: 'POST',
    }).fail(function (jqXHR, textStatus, error) {
        $(".s2_main_container").html('');
        $('#oopsbox').fadeIn();
    })
    .promise();
}

function convertMinitues(minutes) {
    var hours2 = Math.floor(minutes / 60);
    var minutes2 = minutes - (hours2 * 60);

    if (hours2.toString().length == 1) hours2 = '0' + hours2;
    if (minutes2.toString().length == 1) minutes2 = '0' + minutes2;
    if (minutes2 == 0) minutes2 = '00';

    return hours2 + ':' + minutes2;
}

$.getFilterDetail().then(function (obj) {

    var OutboundDepartureTimeMin = parseInt(obj.OutboundDepartureTimeMin);
    var OutboundDepartureTimeMax = parseInt(obj.OutboundDepartureTimeMax);
    var OutboundArrivalTimeMin = parseInt(obj.OutboundArrivalTimeMin);
    var OutboundArrivalTimeMax = parseInt(obj.OutboundArrivalTimeMax);
    var InboundDepartureTimeMin = parseInt(obj.InboundDepartureTimeMin);
    var InboundDepartureTimeMax = parseInt(obj.InboundDepartureTimeMax);
    var InboundArrivalTimeMin = parseInt(obj.InboundArrivalTimeMin);
    var InboundArrivalTimeMax = parseInt(obj.InboundArrivalTimeMax);
    var PriceMin = parseFloat(obj.PriceMin);
    var PriceMax = parseFloat(obj.PriceMax);

    var OutboundDepartureTimeMinSelected = parseInt(obj.OutboundDepartureTimeMinSelected);
    var OutboundDepartureTimeMaxSelected = parseInt(obj.OutboundDepartureTimeMaxSelected);
    var OutboundArrivalTimeMinSelected = parseInt(obj.OutboundArrivalTimeMinSelected);
    var OutboundArrivalTimeMaxSelected = parseInt(obj.OutboundArrivalTimeMaxSelected);
    var InboundDepartureTimeMinSelected = parseInt(obj.InboundDepartureTimeMinSelected);
    var InboundDepartureTimeMaxSelected = parseInt(obj.InboundDepartureTimeMaxSelected);
    var InboundArrivalTimeMinSelected = parseInt(obj.InboundArrivalTimeMinSelected);
    var InboundArrivalTimeMaxSelected = parseInt(obj.InboundArrivalTimeMaxSelected);
    var PriceMinSelected = parseFloat(obj.PriceMinSelected);
    var PriceMaxSelected = parseFloat(obj.PriceMaxSelected);

    $("#outDepMinIni").text(convertMinitues(OutboundDepartureTimeMinSelected > OutboundDepartureTimeMin ? OutboundDepartureTimeMinSelected : OutboundDepartureTimeMin));
    $("#outDepMaxIni").text(convertMinitues(OutboundDepartureTimeMaxSelected < OutboundDepartureTimeMax ? OutboundDepartureTimeMaxSelected : OutboundDepartureTimeMax));
    $("#outArrMinIni").text(convertMinitues(OutboundArrivalTimeMinSelected > OutboundArrivalTimeMin ? OutboundArrivalTimeMinSelected : OutboundArrivalTimeMin));
    $("#outArrMaxIni").text(convertMinitues(OutboundArrivalTimeMaxSelected < OutboundArrivalTimeMax ? OutboundArrivalTimeMaxSelected : OutboundArrivalTimeMax));

    $("#inDepMinIni").text(convertMinitues(InboundDepartureTimeMinSelected > InboundDepartureTimeMin ? InboundDepartureTimeMinSelected : InboundDepartureTimeMin));
    $("#inDepMaxIni").text(convertMinitues(InboundDepartureTimeMaxSelected < InboundDepartureTimeMax ? InboundDepartureTimeMaxSelected : InboundDepartureTimeMax));
    $("#inArrMinIni").text(convertMinitues(InboundArrivalTimeMinSelected > InboundArrivalTimeMin ? InboundArrivalTimeMinSelected : InboundArrivalTimeMin));
    $("#inArrMaxIni").text(convertMinitues(InboundArrivalTimeMaxSelected < InboundArrivalTimeMax ? InboundArrivalTimeMaxSelected : InboundArrivalTimeMax));

    $("#outDepMin").val(OutboundDepartureTimeMinSelected);
    $("#outDepMax").val(OutboundDepartureTimeMaxSelected);
    $("#outArrMin").val(OutboundArrivalTimeMinSelected);
    $("#outArrMax").val(OutboundArrivalTimeMaxSelected);

    $("#inDepMin").val(InboundDepartureTimeMinSelected);
    $("#inDepMax").val(InboundDepartureTimeMaxSelected);
    $("#inArrMin").val(InboundArrivalTimeMinSelected);
    $("#inArrMax").val(InboundArrivalTimeMaxSelected);

    $("#slider-range-out-dep").slider({
        range: true,
        min: OutboundDepartureTimeMin,
        max: OutboundDepartureTimeMax,
        step: 5,
        values: [OutboundDepartureTimeMinSelected, OutboundDepartureTimeMaxSelected],
        change: function (e, ui) {
            filterAirline();
            if ($("#outDepMinIni").text() != convertMinitues(OutboundDepartureTimeMin) || $("#outDepMaxIni").text() != convertMinitues(OutboundDepartureTimeMax)) {
                $(".fillter_tags.outdep").text("Outbound Departure " + $("#outDepMinIni").text() + " - " + $("#outDepMaxIni").text() + " X");
                localStorage.setItem("outbdep", $(".fillter_tags.outdep").text());
            } else {
                localStorage.removeItem("outbdep");
                $(".fillter_tags.outdep").text("");
            }
        },
        slide: function (e, ui) {

            var hours1 = Math.floor(ui.values[0] / 60);
            var minutes1 = ui.values[0] - (hours1 * 60);
            $("#outDepMin").val(ui.values[0]);

            if (hours1.toString().length == 1) hours1 = '0' + hours1;
            if (minutes1.toString().length == 1) minutes1 = '0' + minutes1;
            if (minutes1 == 0) minutes1 = '00';
            //if (hours1 >= 12) {
            //    if (hours1 == 12) {
            //        hours1 = hours1;
            //        minutes1 = minutes1 + " PM";
            //    } else {
            //        hours1 = hours1 - 12;
            //        minutes1 = minutes1 + " PM";
            //    }
            //} else {
            //    hours1 = hours1;
            //    minutes1 = minutes1 + " AM";
            //}
            //if (hours1 == 0) {
            //    hours1 = 12;
            //    minutes1 = minutes1;
            //}



            $('.slider-time').html(hours1 + ':' + minutes1);

            var hours2 = Math.floor(ui.values[1] / 60);
            var minutes2 = ui.values[1] - (hours2 * 60);
            $("#outDepMax").val(ui.values[1]);

            if (hours2.toString().length == 1) hours2 = '0' + hours2;
            if (minutes2.toString().length == 1) minutes2 = '0' + minutes2;
            if (minutes2 == 0) minutes2 = '00';
            //if (hours2 >= 12) {
            //    if (hours2 == 12) {
            //        hours2 = hours2;
            //        minutes2 = minutes2 + " PM";
            //    } else if (hours2 == 24) {
            //        hours2 = 11;
            //        minutes2 = "59 PM";
            //    } else {
            //        hours2 = hours2 - 12;
            //        minutes2 = minutes2 + " PM";
            //    }
            //} else {
            //    hours2 = hours2;
            //    minutes2 = minutes2 + " AM";
            //}

            $('.slider-time2').html(hours2 + ':' + minutes2);
        }
    });

    $("#slider-range-out-arr").slider({
        range: true,
        min: OutboundArrivalTimeMin,
        max: OutboundArrivalTimeMax,
        step: 5,
        values: [OutboundArrivalTimeMinSelected, OutboundArrivalTimeMaxSelected],
        change: function (e, ui) {
            filterAirline();
            if ($("#outArrMinIni").text() != convertMinitues(OutboundArrivalTimeMin) || $("#outArrMaxIni").text() != convertMinitues(OutboundArrivalTimeMax)) {
                $(".fillter_tags.outarr").text("Outbound Arrive " + $("#outArrMinIni").text() + " - " + $("#outArrMaxIni").text() + " X");
                localStorage.setItem("outbarr", $(".fillter_tags.outarr").text());
            } else {
                localStorage.removeItem("outbarr");
                $(".fillter_tags.outarr").text("");
            }
        },
        slide: function (e, ui) {
            var hours1 = Math.floor(ui.values[0] / 60);
            var minutes1 = ui.values[0] - (hours1 * 60);
            $("#outArrMin").val(ui.values[0]);

            if (hours1.toString().length == 1) hours1 = '0' + hours1;
            if (minutes1.toString().length == 1) minutes1 = '0' + minutes1;
            if (minutes1 == 0) minutes1 = '00';
            //if (hours1 >= 12) {
            //    if (hours1 == 12) {
            //        hours1 = hours1;
            //        minutes1 = minutes1 + " PM";
            //    } else {
            //        hours1 = hours1 - 12;
            //        minutes1 = minutes1 + " PM";
            //    }
            //} else {
            //    hours1 = hours1;
            //    minutes1 = minutes1 + " AM";
            //}
            //if (hours1 == 0) {
            //    hours1 = 12;
            //    minutes1 = minutes1;
            //}



            $('.slider-time_1').html(hours1 + ':' + minutes1);

            var hours2 = Math.floor(ui.values[1] / 60);
            var minutes2 = ui.values[1] - (hours2 * 60);
            $("#outArrMax").val(ui.values[1]);

            if (hours2.toString().length == 1) hours2 = '0' + hours2;
            if (minutes2.toString().length == 1) minutes2 = '0' + minutes2;
            if (minutes2 == 0) minutes2 = '00';
            //if (hours2 >= 12) {
            //    if (hours2 == 12) {
            //        hours2 = hours2;
            //        minutes2 = minutes2 + " PM";
            //    } else if (hours2 == 24) {
            //        hours2 = 11;
            //        minutes2 = "59 PM";
            //    } else {
            //        hours2 = hours2 - 12;
            //        minutes2 = minutes2 + " PM";
            //    }
            //} else {
            //    hours2 = hours2;
            //    minutes2 = minutes2 + " AM";
            //}

            $('.slider-time2_1').html(hours2 + ':' + minutes2);
        }
    });

    $("#slider-range-in-dep").slider({
        range: true,
        min: InboundDepartureTimeMin,
        max: InboundDepartureTimeMax,
        step: 5,
        values: [InboundDepartureTimeMinSelected, InboundDepartureTimeMaxSelected],
        change: function (e, ui) {
            filterAirline();
            if ($("#inDepMinIni").text() != convertMinitues(InboundDepartureTimeMin) || $("#inDepMaxIni").text() != convertMinitues(InboundDepartureTimeMax)) {
                $(".fillter_tags.indep").text("Inbound Departure " + $("#inDepMinIni").text() + " - " + $("#inDepMaxIni").text() + " X");
                localStorage.setItem("inbdep", $(".fillter_tags.indep").text());
            } else {
                localStorage.removeItem("inbdep");
                $(".fillter_tags.indep").text("");
            }
        },
        slide: function (e, ui) {
            var hours1 = Math.floor(ui.values[0] / 60);
            var minutes1 = ui.values[0] - (hours1 * 60);
            $("#inDepMin").val(ui.values[0]);

            if (hours1.toString().length == 1) hours1 = '0' + hours1;
            if (minutes1.toString().length == 1) minutes1 = '0' + minutes1;
            if (minutes1 == 0) minutes1 = '00';
            //if (hours1 >= 12) {
            //    if (hours1 == 12) {
            //        hours1 = hours1;
            //        minutes1 = minutes1 + " PM";
            //    } else {
            //        hours1 = hours1 - 12;
            //        minutes1 = minutes1 + " PM";
            //    }
            //} else {
            //    hours1 = hours1;
            //    minutes1 = minutes1 + " AM";
            //}
            //if (hours1 == 0) {
            //    hours1 = 12;
            //    minutes1 = minutes1;
            //}



            $('.slider-time_2').html(hours1 + ':' + minutes1);

            var hours2 = Math.floor(ui.values[1] / 60);
            var minutes2 = ui.values[1] - (hours2 * 60);
            $("#inDepMax").val(ui.values[1]);

            if (hours2.toString().length == 1) hours2 = '0' + hours2;
            if (minutes2.toString().length == 1) minutes2 = '0' + minutes2;
            if (minutes2 == 0) minutes2 = '00';
            //if (hours2 >= 12) {
            //    if (hours2 == 12) {
            //        hours2 = hours2;
            //        minutes2 = minutes2 + " PM";
            //    } else if (hours2 == 24) {
            //        hours2 = 11;
            //        minutes2 = "59 PM";
            //    } else {
            //        hours2 = hours2 - 12;
            //        minutes2 = minutes2 + " PM";
            //    }
            //} else {
            //    hours2 = hours2;
            //    minutes2 = minutes2 + " AM";
            //}

            $('.slider-time2_2').html(hours2 + ':' + minutes2);
        }
    });

    $("#slider-range-in-arr").slider({
        range: true,
        min: InboundArrivalTimeMin,
        max: InboundArrivalTimeMax,
        step: 5,
        values: [InboundArrivalTimeMinSelected, InboundArrivalTimeMaxSelected],
        change: function (e, ui) {
            filterAirline();
            if ($("#inArrMinIni").text() != convertMinitues(InboundArrivalTimeMin) || $("#inArrMaxIni").text() != convertMinitues(InboundArrivalTimeMax)) {
                $(".fillter_tags.inarr").text("Inbound Arrive " + $("#inArrMinIni").text() + " - " + $("#inArrMaxIni").text() + " X");
                localStorage.setItem("inbarr", $(".fillter_tags.inarr").text());
            } else {
                localStorage.removeItem("inbarr");
                $(".fillter_tags.inarr").text("");
            }
        },
        slide: function (e, ui) {
            var hours1 = Math.floor(ui.values[0] / 60);
            var minutes1 = ui.values[0] - (hours1 * 60);
            $("#inArrMin").val(ui.values[0]);

            if (hours1.toString().length == 1) hours1 = '0' + hours1;
            if (minutes1.toString().length == 1) minutes1 = '0' + minutes1;
            if (minutes1 == 0) minutes1 = '00';
            //if (hours1 >= 12) {
            //    if (hours1 == 12) {
            //        hours1 = hours1;
            //        minutes1 = minutes1 + " PM";
            //    } else {
            //        hours1 = hours1 - 12;
            //        minutes1 = minutes1 + " PM";
            //    }
            //} else {
            //    hours1 = hours1;
            //    minutes1 = minutes1 + " AM";
            //}
            //if (hours1 == 0) {
            //    hours1 = 12;
            //    minutes1 = minutes1;
            //}



            $('.slider-time_3').html(hours1 + ':' + minutes1);

            var hours2 = Math.floor(ui.values[1] / 60);
            var minutes2 = ui.values[1] - (hours2 * 60);
            $("#inArrMax").val(ui.values[1]);

            if (hours2.toString().length == 1) hours2 = '0' + hours2;
            if (minutes2.toString().length == 1) minutes2 = '0' + minutes2;
            if (minutes2 == 0) minutes2 = '00';
            //if (hours2 >= 12) {
            //    if (hours2 == 12) {
            //        hours2 = hours2;
            //        minutes2 = minutes2 + " PM";
            //    } else if (hours2 == 24) {
            //        hours2 = 11;
            //        minutes2 = "59 PM";
            //    } else {
            //        hours2 = hours2 - 12;
            //        minutes2 = minutes2 + " PM";
            //    }
            //} else {
            //    hours2 = hours2;
            //    minutes2 = minutes2 + " AM";
            //}

            $('.slider-time2_3').html(hours2 + ':' + minutes2);
        }
    });

    $(function () {
        $("#slider-range").slider({
            range: true,
            min: PriceMin,
            max: PriceMax,
            step: 0.01,
            values: [PriceMinSelected, PriceMaxSelected],
            change: function (event, ui) {
                filterAirline();
                //if (ui.values[0] > PriceMin && ui.values[1] == PriceMax) {
                //    $(".fillter_tags.Bottom_yellow_bg").text("Price > MYR" + ui.values[0] + " X");
                //    localStorage.setItem("pricefil", $(".fillter_tags.Bottom_yellow_bg").text());
                //} else if (ui.values[0] == PriceMin && ui.values[1] < PriceMax) {
                //    $(".fillter_tags.Bottom_yellow_bg").text("Price < MYR" + ui.values[1] + " X");
                //    localStorage.setItem("pricefil", $(".fillter_tags.Bottom_yellow_bg").text());
                //} else
                if (ui.values[0] > PriceMin || ui.values[1] < PriceMax) {
                    $(".fillter_tags.Bottom_yellow_bg").text("Price MYR" + ui.values[0] + " - MYR" + ui.values[1] + " X");
                    localStorage.setItem("pricefil", $(".fillter_tags.Bottom_yellow_bg").text());
                } else {
                    $(".fillter_tags.Bottom_yellow_bg").text("");
                    localStorage.removeItem("pricefil");
                }
            },
            slide: function (event, ui) {
                var miniPrice = parseFloat(ui.values[0]).toFixed(2);
                var maxiPrice = parseFloat(ui.values[1]).toFixed(2);
                $("#amount").html("MYR<span>" + miniPrice + "</span> - MYR<span>" + maxiPrice + "</span>");
                $('#FilterFlightModel_PriceMin').val(ui.values[0]);
                $('#FilterFlightModel_PriceMax').val(ui.values[1]);
            }
        });
        var sliderPriceMin = $("#slider-range").slider("values", 0);
        var miniPrice = parseFloat(sliderPriceMin).toFixed(2);
        var sliderPriceMax = $("#slider-range").slider("values", 1);
        var maxiPrice = parseFloat(sliderPriceMax).toFixed(2);
        $("#amount").html("MYR<span>" + miniPrice +
         "</span> - MYR<span>" + maxiPrice + "</span>");
        $('#FilterFlightModel_PriceMin').val(sliderPriceMin);
        $('#FilterFlightModel_PriceMax').val(sliderPriceMax);
    });
});
