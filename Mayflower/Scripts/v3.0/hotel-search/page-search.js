$(function () {
    var starLoaded = false;
    var isSliding = false;

    var hotel = function () {
        var minRate = 0;
        var maxRate = 0;
        var totalProperty = 0;

        var dfd = new $.Deferred();

        getFilterPanelInfo();

        var _obj = this;
        dfd.done(function (res) {
            _obj.minRate = res.minRate;
            _obj.maxRate = res.maxRate;
            _obj.totalProperty = res.totalProperty;
            _obj.res = res;
            return res;
        }).then(function (res) {
            $("#FltPriceFrom").val(res.fltPriceFrom);
            $('.ht-filter-pricemin').html(Math.floor(res.fixedminRate == 0 ? res.minRate : res.fixedminRate));
            $('.ht-filter-pricemax').html(Math.ceil(res.fixedmaxRate == 0 ? res.maxRate : res.fixedmaxRate));

            $('.input-pr.prmin').val(Math.floor(res.fixedfilterMin == 0 ? res.filter.filterMin : res.fixedfilterMin));
            $('.input-pr.prmax').val(Math.ceil(res.fixedfilterMax == 0 ? res.filter.filterMax : res.fixedfilterMax));
            $('.input-pr.prmin').attr('min', Math.floor(res.minRate));
            $('.input-pr.prmax').attr('max', Math.ceil(res.maxRate));

            $("#price-slider").attr('data-slider-min', Math.floor(res.minRate));
            $("#price-slider").attr('data-slider-max', Math.ceil(res.maxRate));
            $("#price-slider").attr('data-slider-value', '[' + Math.floor(res.filter.filterMin) + ',' + Math.ceil(res.filter.filterMax) + ']');
            $("#price-slider").slider('destroy');
            if (document.getElementById("FixedPaxNo")) {
                $("#price-slider").slider({
                    tooltip: 'hide',
                });
            } else {
                $("#price-slider").slider({});
            }
            initSliderEv();

            // star property count
            /*for (var i = 0; i <= 5; i++) {
                $('[for="star-' + i + '"]')
                    .parents('.filter-star-container').children('label').last().remove();
            }*/

            if (res.starList !== null && !starLoaded) {
                for (var i = 0; i < res.starList.length; i++) {
                    if (i === 0) {
                        $('[for="star-' + 0 + '"]')
                            .parents('.filter-star-container')
                            .append('<label class="pull-right">&nbsp;&nbsp;(' + 0 + ')</label>');
                    }

                    var _p = $('[for="star-' + res.starList[i].R + '"]').parents('.filter-star-container');
                    var _qp = _p.children('label').last();
                    if (_qp.length === 0) {
                        _p.append('<label class="pull-right">&nbsp;&nbsp;(' + res.starList[i].C + ')</label>');
                    }
                    else {
                        _qp.html('&nbsp;&nbsp;(' + res.starList[i].C + ')')
                    }

                }
                starLoaded = true;
            }

            // star filter
            if (res.filter.filterStar !== null) {
                var _star = res.filter.filterStar.split(',');

                for (var b = 0; b < _star.length; b++) {
                    $('.filter-star-input[value=' + _star[b] + ']').prop('checked', 'checked');
                }
            }

            // property filter
            $('#inlineFormPropertySearch').val(res.filter.filterProperty);

            // locationFilter
            if (res.locDesc !== null && res.locDesc.length && $('.ht-filter-location option').length === 1) {
                for (var a = 0; a < res.locDesc.length; a++) {
                    $('.ht-filter-location').append('<option value="' + res.locDesc[a] + '">' + res.locDesc[a] + '</option>');
                }
            }

            if ($('.ht-filter-location option').length > 1 && typeof (res.filter.filterLoc) !== 'undefined' && res.filter.filterLoc !== null) {
                $('.ht-filter-location [value="' + res.filter.filterLoc + '"]').attr('selected', true);
            }

            // sorting assign
            if (res.sortType === null) {
                $($('[data-ht-sorting]')[0]).addClass('active');
            }
            else {
                $('[data-ht-sorting]').removeClass('active');
                $('[data-ht-sorting="' + res.sortType + '"]').addClass('active');

                if (res.sortSq === null) {
                }
                else {
                    $('[data-ht-sorting="' + res.sortType + '"]').addClass(res.sortSq);
                }
            }

        }, function (resF) {
        });

        this.minRate = minRate;
        this.maxRate = maxRate;
        this.totalProperty = totalProperty;

        function getFilterPanelInfo() {
            $.ajax({
                type: "POST",
                cache: false,
                dataType: 'json',
                url: '/hotel/getfilterpanelinfo' + location.search,
            }).then(function (res) {
                dfd.resolve(res);
            }, function (res) {
                console.log(res);
            });
        }
    }

    var getHList = function (pg, data) {
        var q = (location.search.indexOf('?') === -1 ? '?version=v2' : '&version=v2')

        if (typeof (pg) !== 'undefined')
            q += '&page=' + pg;
        else
            q += '&newsearch=1';

        //$('.ht-loading').toggle();
        var gthtltimeout = $("#IsFixedPrice").val() == "True" ? 180000 : 120000; //3 minutes / 2 minutes.
        $.ajax({
            type: "POST",
            cache: false,
            dataType: 'html',
            data: typeof (data) !== 'undefined' ? data : $('form').serialize(),
            url: '/hotel/gethotellist' + location.search + q,
            timeout: gthtltimeout,
        }).then(function (res) {
            $('.hlist-container').html(res);
            if (typeof (pg) !== 'undefined') { // scroll only when not new search
                stEle('.hlist-container'); // smooth scroll to container
            }
            h = new hotel();
            upPc();
        }, function (res) {
            console.log(res);
            $('.hlist-container').html('Unexpected error, please try again later.');
        }).done(function (res) {
            $('.ht-loading').remove();
        });
    }

    var filterNow = function (clrPrVal) {
        var _sliderVal = $('#price-slider').slider('getValue');
        var min = 0;
        var max = 999999;
        var _star = null;
        var _location = $('.ht-filter-location option:selected').text();

        if (_sliderVal.length > 1) {
            min = _sliderVal[0];
            max = _sliderVal[1];
        }

        $('.filter-star-input:checked').each(function (i, e) {
            if (_star === null) {
                _star = [];
            }
            _star[i] = e.value;
        });

        if (_star !== null) {
            _star = _star.sort();
            _star = _star.join(',');
        }
        else {
            _star = '10';
        }

        if (_location === '-') {
            _location = null;
        }

        var data = {
            ddlSorting: $('[data-ht-sorting].active').data('ht-sort'),
            sortSeq: $('[data-ht-sorting].active').data('ht-sortsq'),
            hidRating: _star,
            SelectedMinPrice: min,
            SelectedMaxPrice: max,
            Property: $('.ht-filter-property').val(),
            LocationNearBy: _location,
            hidTax: $('.ht-display-includeTax').is(':checked'),
            hidAllNights: $('.ht-display-allNight').is(':checked'),
            clrPrVal: typeof (clrPrVal) !== 'undefined' && clrPrVal === true ? '1' : null,
        };
        getHList(1, data, clrPrVal);
    };

    var upPc = function () {
        $('.ht-price-supp').each(function (i, e) {
            var htdt = $(e).data('hid');
            gtPc(htdt);
        });
    }

    var gtPc = function (ht) {
        $.ajax({
            url: '/spagent/ahotel/_gtpc' + location.search,
            dataType: 'json',
            async: true,
            data: {
                htid: ht
            },
            method: 'get',
            complete: function (res) {
                var lowest = 0;
                var curr = '';
                var pList = $('ul.ht-price-supp[data-hid="' + ht + '"]').parent().parent();

                if (typeof (res.responseJSON) === 'undefined') {
                    return false;
                }
                var pPrice = pList.find('.ht-desc-price');

                pList = pList.find('ul.ht-price-supp');

                pList.find('li').each(function (i, e) {
                    $(e).addClass('disabled');
                });

                $(res.responseJSON).each(function (i, e) {
                    if (e.Price < lowest || lowest == 0) {
                        lowest = e.Price;
                        curr = e.Curr;

                        var _parseVal = (lowest.toFixed(2));
                        pPrice.html(numeral(_parseVal).format('0,0.00'));
                    }

                    var pcBlock = pList.find('li[data-supp="' + e.Source + '"]');
                    pcBlock.attr('data-price', e.Price);
                    pcBlock.attr('data-hotelid', e.HID);
                    pcBlock.addClass('room-trigger');
                    pcBlock.removeClass('disabled');
                });
            },
        });
    }

    var initSliderEv = function () {
        if ($('#price-slider').length > 0) {
            $('#price-slider').slider().on('slide', function (e) {
                isSliding = true;
                if (document.getElementById("FixedPaxNo")) {
                    $('.input-pr.prmin').val(((e.value[0] * parseInt($("[name='NoOfRoom']").val())) + parseInt($("#FltPriceFrom").val())) / parseInt($("#FixedPaxNo").val()));
                    $('.input-pr.prmax').val(((e.value[1] * parseInt($("[name='NoOfRoom']").val()))+ parseInt($("#FltPriceFrom").val())) / parseInt($("#FixedPaxNo").val()));
                } else {
                    $('.input-pr.prmin').val(e.value[0]);
                    $('.input-pr.prmax').val(e.value[1]);
                }
            });

            $('#price-slider').slider().on('slideStop', function (e) {
                isSliding = false;
                filterNow();
            });
        }
    }

    $('.ht-filter-property').on('keydown', function (e) {
        if (e.keyCode === 13) {
            filterNow(true);
        }
    });

    $('.ht-ft-price .input-pr').on('change', function (e) {
        if (!isSliding) {
            var _v0 = parseInt($('.input-pr.prmin').val());
            var _v1 = parseInt($('.input-pr.prmax').val());
            if (document.getElementById("FixedPaxNo")) {
                _v0 = (_v0 * parseInt($("#FixedPaxNo").val())) - parseInt($("#FltPriceFrom").val());
                _v1 = (_v1 * parseInt($("#FixedPaxNo").val())) - parseInt($("#FltPriceFrom").val());
            }
            $('#price-slider').slider('setValue', [_v0, _v1])

            filterNow();
        }
    });

    $('.ht-filter-btn-property').on('click', function (e) {
        filterNow(true);
    });

    $('.ht-filter-location').on('change', function (e) {
        filterNow(true);
    });

    $('.filter-star-input').on('change', function (e) {
        filterNow(true);
    });

    $('.ht-display-setting').on('change', function (e) {
        filterNow(true);
    });

    if ($('.hlist-container').length === 1 && $('.hlist-container').html().trim().length === 0) {
        getHList();
    }

    if ($('.req-supp-pc').length > 0) {
        upPc();
    }

    $('.hlist-container').on('click', '[data-page-no]', function (e) {
        var pg = $(this).data('page-no');
        getHList(pg);
    });

    $('.ht-sort-container').on('click', '[data-ht-sorting]', function (e) {
        if ($(this).hasClass('active')) {
            if ($(this).data('ht-sortsq') == 0) {
                $(this).data('ht-sortsq', 1);
            }
            else {
                $(this).data('ht-sortsq', 0);
            }
            $(this).toggleClass('asc desc');
            filterNow();
            return;
        }

        $('[data-ht-sorting]').removeClass('active desc');
        $(this).data('sortSeq', 0);
        $(this).addClass('active asc');
        filterNow();
    });

    $('.hlist-container').on('click', '.room-trigger', function (e) {
        e.preventDefault();

        var _btn = $(this);
        var cEle = _btn.parents('.search-item');
        var pEle = cEle.find('.room-container');
        var act = _btn.parent().find('.room-trigger.active');
        var isAct = _btn.hasClass('active');
        var gtroomurl = $("#IsFixedPrice").val() == "True" ? '/Hotel/GetFixedRoom' : '/Hotel/GtRoom';

        hsInfo.HID = _btn.data("hotelid");
        hsInfo.HSR = _btn.data('supp');
        hsInfo.TKR = _btn.data('rtoken');
        hsInfo.lowRate = _btn.parent().find('.ht-desc-price').text();

        var isAg = _btn.parent().hasClass('ht-price-supp');
        var terminate = false;
        var hideTrpAdv = false;

        if (_btn.hasClass('btn-primary')) {
            closeAllRoomList();
            stEle(pEle);
        }
        else {
            stEle(cEle);
        }

        if (isAct && _btn.parent().hasClass('ht-price-supp') && pEle.find('.room-wrapper').length > 0) {
            pEle.find('.room-wrapper').toggle(200);
            _btn.toggleClass('active');
            hideTrpAdv = _btn.hasClass('active');
            terminate = true;
        }
        else if (!_btn.parent().hasClass('ht-price-supp') && pEle.find('.room-wrapper').length > 0) {
            pEle.find('.room-wrapper').toggle(200);
            _btn.toggleClass('btn-primary btn-secondary');
            hideTrpAdv = _btn.hasClass('btn-primary');
            terminate = true;
        }

        if (terminate) {
            var trpCtn = pEle.siblings('.ht-trpadv-container');
            var trpFrm = trpCtn.find('iframe');
            if (trpFrm.attr('src') === '') {
                trpFrm.attr('src', trpFrm.data('src'));
            }

            if (!hideTrpAdv) {
                trpCtn.fadeIn();
            }
            else {
                trpCtn.fadeOut();
            }
            return;
        }

        for (var i = 0; i < act.length; i++) {
            $(act[i]).removeClass('active');
        }

        $.ajax({
            type: "GET",
            url: gtroomurl + location.search,
            data: hsInfo,
            cache: false,
            async: true,
            dataType: "html",
            timeout: 90000, // 1.30 minutes.
            beforeSend: function () {
                loadModal(true);
                var trpCtn = pEle.siblings('.ht-trpadv-container');
                var trpFrm = trpCtn.find('iframe');
                if (trpFrm.attr('src') === '') {
                    trpFrm.attr('src', trpFrm.data('src'));
                }
                trpCtn.fadeIn();
            },
        }).done(function (res) {
                pEle.html(res);
            pEle.find('.room-wrapper').toggle(200);

            if (isAg) {
                _btn.toggleClass('active');
            }
            else {
                _btn.toggleClass('btn-primary btn-secondary');
            }
            $('[data-toggle="popover"]').popover({
                trigger: 'focus'
            });

            loadModal(false);
        }).fail(function (res) {
            loadModal(false);
            dynamicModal('Room', 'Unexpected error occur, please try again later.', false, true).modal();
            stEle(cEle); // if failed scroll to hotel
        });
    });

    $('.hlist-container').on('click', '.room-select-btn', function (e) {
        if (!$(this).parents('.hlist-container').hasClass('HTL')) {
            checkRoomInv(this);
        }
    });

    $('.hlist-container').on('click', '.room-link-btn', function (e) {
        if (!$(this).parents('.hlist-container').hasClass('HTL')) {
            generateHtlLink(this);
        }
    });

    /*START: get location on google map*/
    $('.hlist-container').on('click', '.show-map', function () {
        $.ajax({
            type: "GET",
            url: '/hotel/getlocationonmap_v2' + location.search + '&name=' + $(this).data("name") + '&city=' + $(this).data("city"),
            cache: false,
            async: true,
            dataType: "text",
            beforeSend: function () {
                dynamicModal('Map', loadImg(), false, true).modal();
            }
        }).done(function (result) {
            dynamicModal('Map', result, false, true).modal();
        }).fail(function () {
            dynamicModal('Map', 'Unexpected error occur, please try again later.', false, true).modal();
        });
    });
    /*END: get location on google map*/

    /* START: get tripadvisor */
    $('.hlist-container').on('click', '.show-review', function () {
        var dt = {
            lid: $(this).data('ht-lid'),
            lat: $(this).data('ht-lat'),
            lon: $(this).data('ht-lon'),
            sr: $(this).data('ht-sr'),
            id: $(this).data('ht-id')
        };
        $.ajax({
            type: "GET",
            url: '/hotel/gettripadvisorreview' + location.search,
            data: dt,
            cache: false,
            async: true,
            dataType: "text",
            beforeSend: function () {
                dynamicModal('Reviews', loadImg(), false, true).modal();
            }
        }).done(function (result) {
            dynamicModal('Reviews', result, false, true).modal();
        }).fail(function () {
            dynamicModal('Reviews', 'Unexpected error occur, please try again later.', false, true).modal();
        });
    });
    /* END: get tripadvisor */

    /* START: get tripadvisor nearby */
    $('.hlist-container').on('click', '.show-tr-nearby', function () {
        var dt = {
            lid: $(this).data('ht-lid'),
            lat: $(this).data('ht-lat'),
            lon: $(this).data('ht-lon'),
            sr: $(this).data('ht-sr'),
            id: $(this).data('ht-id')
        };
        $.ajax({
            type: "GET",
            url: '/hotel/gettripadvisornearby' + location.search,
            data: dt,
            cache: false,
            async: true,
            dataType: "text",
            beforeSend: function () {
                dynamicModal('Nearby', loadImg(), false, true).modal();
            }
        }).done(function (result) {
            dynamicModal('Nearby', result, false, true).modal();
        }).fail(function () {
            dynamicModal('Nearby', 'Unexpected error occur, please try again later.', false, true).modal();
        });
    });
    /* END: get tripadvisor */


    /* START: get tripadvisor nearby */
    $('.hlist-container,.reservation-body').on('click', '.show-ht-info', function () {
        var dt = {
            sr: $(this).data('ht-sr'),
            id: $(this).data('ht-id')
        };
        $.ajax({
            type: "GET",
            url: '/hotel/gethotelinfo_v2' + location.search,
            data: dt,
            cache: false,
            async: true,
            dataType: "text",
            beforeSend: function () {
                dynamicModal('Hotel Information', loadImg(), false, true).modal();
            }
        }).done(function (result) {
            dynamicModal('Hotel Information', result, false, true).modal();
        }).fail(function () {
            dynamicModal('Hotel Information', 'Unexpected error occur, please try again later.', false, true).modal();
        });
    });
    /* END: get tripadvisor */

    /* START: TripAdvisor Summary Widget */
    $('.search-result').on('click', '.ht-trpadv-container .close', function (e) {
        $(this).parents('.ht-trpadv-container').fadeOut();
    });
    /* END: TripAdvisor Summary Widget */

    var closeAllRoomList = function (elNotClose) {
        $('.room-trigger.btn-secondary').each(function (i, e) {
            $(e).toggleClass('btn-primary btn-secondary');
            var el = $(e).parents('.search-item').find('.room-container');
            el.find('.room-wrapper').toggle(0);
            el.siblings('.ht-trpadv-container').fadeOut();
        });
    }

    var pushRoomToCart = function (e) {
        var rd = {
            hotel: $(e).data('hid'),
            typeCode: $(e).attr("data-roomtypecode"),
            rateCode: $(e).attr("data-ratecode"),
            qty: $(e).attr("data-qty"),
            name: $(e).data('roomname'),
            propertyId: $(e).data("propertyid"),
            ratetype: $(e).attr("data-ratetype"),
            ratetoken: $(e).attr("data-ratetoken"),
            encSupp: $(e).attr("data-encsupp"),
        };
        return rd;
    }

    var checkRoomInv = function (element) {
        var r = [];

        try {
            var NameList = "";
            r.push(pushRoomToCart(element));

        } catch (e) { console.log(e); }

        var obj = "";
        var data;

        var request = $.Deferred();
        if (NameList.length > 0 || r.length > 0) {
            request = $.ajax({
                type: "POST",
                url: '/Hotel/CheckInventory' + location.search,
                data: JSON.stringify(r),
                cache: false,
                async: true,
                dataType: 'json',
                contentType: 'application/json',
                beforeSend: function () {
                    //$('#loading-modal').show();
                }
            }).promise();
        }
        else {
            data = "NOROOM";
        }

        request.done(function (result) {
            //$('#loading-modal').hide();
            if (Array.isArray(result)) {
                result = "ENOUGH";
            }
            data = typeof result === "undefined" ? data : result;

            switch (data) {
                case "NOTENOUGH": obj = 'Total number of room(s) selected is unable to cater for the total travelers inserted, please select again to proceed. Thank you.'; break;
                case "NOROOM": obj = 'Please select room first.'; break;
                case "TIMEOUT": obj = 'Your search session has expired.'; break;
                case "EXCEED": obj = '<h2 style="color:#be2e30">Thank you for choosing to travel with Mayflower.</h2><br /><br />' +
                    'Your selected number of room(s) has exceeded the maximum limit; <br /> <br />please contact our Customer Service Team &#64; email: cs@mayflower-group.com to proceed further.< br /> <br />' +
                    'Alternatively, kindly segregate your booking and try again.'; break;
                case "EXCEEDSEARCH": obj = '<h2 style="color:#be2e30">Thank you for choosing to travel with Mayflower.</h2><br /><br />' +
                    'Your selected number of room(s) has exceeded the number of room(s) you searched; <br /> <br />please contact our Customer Service Team &#64; email: cs@mayflower-group.com to proceed further.< br /> <br />' +
                    'Alternatively, kindly segregate your booking and try again.'; break;
                default:
                    if (data.length > 10) {
                        obj = data;
                    }
                    break;
            }
            if (data == "TIMEOUT") {
                $('#genericModal .btn.modal-close').addClass("sessionexp_close");
                $('#genericModal .btn.modal-confirm').text("start again");
                $('#genericModal .btn.modal-confirm').addClass("btn-secondary");
                $('#genericModal .btn.modal-confirm').attr('onClick', 'window.location = "/";');
                dynamicModal('Error', obj, true).modal();
                return;
            }
            else if (data !== "ENOUGH") {
                $('#genericModal .btn.modal-close').removeClass("sessionexp_close");
                dynamicModal('Error', obj, false).modal();
                return;
            }

            var url = $('input[type="hidden"].submit').data('url');
            $('<form action="' + url + '" method="POST">' +
                '<input hidden="key" name="key" value="' + encodeURIComponent(JSON.stringify(r)) + '" />' +
                '</form>')
                .appendTo($(document.body))
                .submit();

            return;
        });

        if (!(NameList.length > 0)) {
            request.resolve();
        }
    }

    var generateHtlLink = function (element) {
        var r = [];

        try {
            var NameList = "";
            r.push(pushRoomToCart(element));

        } catch (e) { console.log(e); }

        var obj = "";
        var data;

        var request = $.Deferred();
        if (NameList.length > 0 || r.length > 0) {
            request = $.ajax({
                type: "POST",
                url: '/Hotel/GenerateHotelLink' + location.search,
                data: JSON.stringify(r),
                cache: false,
                async: true,
                dataType: 'json',
                contentType: 'application/json',
                beforeSend: function () {
                    //$('#loading-modal').show();
                }
            }).promise();
        }
        else {
            data = "NOROOM";
        }

        request.done(function (result) {
            //$('#loading-modal').hide();
            data = typeof result === "undefined" ? data : result;

            switch (data.flag) {
                case "NOROOM": obj = 'Please select room first.'; break;
                case "EXCEEDSEARCH": obj = '<h2 style="color:#be2e30">Thank you for choosing to travel with Mayflower.</h2><br /><br />' +
                    'Your selected number of room(s) has exceeded the number of room(s) you searched; <br /> <br />please contact our Customer Service Team &#64; email: cs@mayflower-group.com to proceed further.<br /><br />' +
                    'Alternatively, kindly segregate your booking and try again.'; break;
                case "SAVESUCCESS": obj = 'The link of your selected hotel is <br/><br/><span style="word-wrap:break-word;">' + data.link + '</span>'; break;
                default:
                    if (data.flag.length > 10) {
                        obj = data.flag;
                    }
                    break;
            }

            if (data.flag != "SAVEFAIL") {
                dynamicModal('Hotel Link', obj, false).modal();
                return;
            } else {
                dynamicModal('Error', obj, false).modal();
            }
            return;
        });
        
        if (!(NameList.length > 0)) {
            request.resolve();
        }
    }

    if ($("#price-slider").length) {
        var h = new hotel();
        initSliderEv();
    }
});
