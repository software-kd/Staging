var width = $(window).width(), height = $(window).height();

(function ($) {

    $.fn.menumaker = function (options) {

        var cssmenu = $(this), settings = $.extend({
            title: "Menu",
            format: "dropdown",
            sticky: false
        }, options);

        return this.each(function () {
            cssmenu.prepend('<div id="menu-button">' + settings.title + '</div>');
            $(this).find("#menu-button").on('click', function () {
                $(this).toggleClass('menu-opened');
                var mainmenu = $(this).next('ul');
                if (mainmenu.hasClass('open')) {
                    mainmenu.hide().removeClass('open');
                }
                else {
                    mainmenu.show().addClass('open');
                    if (settings.format === "dropdown") {
                        mainmenu.find('ul').show();
                    }
                }
            });

            cssmenu.find('li ul').parent().addClass('has-sub');

            multiTg = function () {
                cssmenu.find(".has-sub").prepend('<span class="submenu-button"></span>');
                cssmenu.find('.submenu-button').on('click', function () {
                    $(this).toggleClass('submenu-opened');
                    if ($(this).siblings('ul').hasClass('open')) {
                        $(this).siblings('ul').removeClass('open').hide();
                    }
                    else {
                        $(this).siblings('ul').addClass('open').show();
                    }
                });
            };

            if (settings.format === 'multitoggle') multiTg();
            else cssmenu.addClass('dropdown');

            if (settings.sticky === true) cssmenu.css('position', 'fixed');

            resizeFix = function () {
                //Lee Zein Khai 30-5-2017 start//
                if ($(window).width() <= 900 && $(window).width() != width) {
                    $('#menu-button').removeClass('menu-opened');
                    $('.submenu-opened').removeClass('submenu-opened');
                    cssmenu.find('ul').removeClass('open');
                    //cssmenu.find('ul').hide().removeClass('open');
                }
                //Lee Zein Khai 30-5-2017 end//
            };
            resizeFix();
            return $(window).on('resize', resizeFix);




        });
    };
})(jQuery);

(function ($) {
    $(document).ready(function () {

        $("#cssmenu").menumaker({
            title: "",
            format: "multitoggle"
        });

        $('#cssmenu ul > li.has-sub span').unbind('click');

        $('#cssmenu ul > li.has-sub').on('click', function (e) {
            var btn = $(this).find('span');
            btn.toggleClass('submenu-opened');
            if (btn.siblings('ul').hasClass('open')) {
                btn.siblings('ul').removeClass('open').hide();
            }
            else {
                btn.siblings('ul').addClass('open').show();
            }
        });
    });
})(jQuery);


$(window).resize(function () {
        var resizeReal = ($(window).width() != width && $(window).height() != height);
        if ($(window).width() < 900) {
            if ($('.newcontainer1').css('display') != 'block' && $('.newcontainer2').css('display') != 'block' && $('.newcontainer3').css('display') != 'block' && $('.newcontainer4').css('display') != 'block') {
                //$('.flight_filter_tag.filtertag > .fft_right').removeClass('fft_right_collapsed');
            }
            if (!$('.flight_filter_tag.sorttag_price > .fft_right').hasClass('fft_right_collapsed')){
                $('.map_bestdeal_container').hide();
            }
            if ($('.newcontainer1').css('display') == 'block' || $('.tabsformobile1').hasClass("newclassfortab1a")) {
                $(".tabsformobile1").show();
                $('.newcontainer1').show();
                $('.flight_filter_tag.filtertag > .fft_right').addClass('fft_right_collapsed');
                $('.tabsformobile1').addClass("newclassfortab1a");
                $(".tabsformobile2").show();
                $('.tabsformobile2').removeClass("newclassfortab2a");
                $(".tabsformobile3").show();
                $('.tabsformobile3').removeClass("newclassfortab3a");
                $(".tabsformobile4").show();
                $('.tabsformobile4').removeClass("newclassfortab4a");
            }
            if ($('.newcontainer2').css('display') == 'block' || $('.tabsformobile2').hasClass("newclassfortab2a")) {
                $(".tabsformobile1").show();
                $('.newcontainer2').show();
                $('.flight_filter_tag.filtertag > .fft_right').addClass('fft_right_collapsed');
                $('.tabsformobile1').removeClass("newclassfortab1a");
                $(".tabsformobile2").show();
                $('.tabsformobile2').addClass("newclassfortab2a");
                $(".tabsformobile3").show();
                $('.tabsformobile3').removeClass("newclassfortab3a");
                $(".tabsformobile4").show();
                $('.tabsformobile4').removeClass("newclassfortab4a");
            }
            if ($('.newcontainer3').css('display') == 'block' || $('.tabsformobile3').hasClass("newclassfortab3a")) {
                $(".tabsformobile1").show();
                $('.newcontainer3').show();
                $('.flight_filter_tag.filtertag > .fft_right').addClass('fft_right_collapsed');
                $('.tabsformobile1').removeClass("newclassfortab1a");
                $(".tabsformobile2").show();
                $('.tabsformobile2').removeClass("newclassfortab2a");
                $(".tabsformobile3").show();
                $('.tabsformobile3').addClass("newclassfortab3a");
                $(".tabsformobile4").show();
                $('.tabsformobile4').removeClass("newclassfortab4a");
            }
            if ($('.newcontainer4').css('display') == 'block' || $('.tabsformobile4').hasClass("newclassfortab4a")) {
                $(".tabsformobile1").show();
                $('.newcontainer4').show();
                $('.flight_filter_tag.filtertag > .fft_right').addClass('fft_right_collapsed');
                $('.tabsformobile1').removeClass("newclassfortab1a");
                $(".tabsformobile2").show();
                $('.tabsformobile2').removeClass("newclassfortab2a");
                $(".tabsformobile3").show();
                $('.tabsformobile3').removeClass("newclassfortab3a");
                $(".tabsformobile4").show();
                $('.tabsformobile4').addClass("newclassfortab4a");
            }
        } else if ($(window).width() > 900) {
            $(".tabsformobile1").hide();
            $(".tabsformobile2").hide();
            $(".tabsformobile3").hide();
            $(".tabsformobile4").hide();
            $(".s2_fil_in_right_sm").hide();
            if ($('.newcontainer1').css('display') == 'block' || $('.newcontainer2').css('display') == 'block' || $('.newcontainer3').css('display') == 'block' || $('.newcontainer4').css('display') == 'block') {
                $(".hidefilter_icon").show();
            } else {
                $(".hidefilter_icon").hide();
                $(".tabfil1").removeClass("newclassfortab1");
                $(".tabfil2").removeClass("newclassfortab2");
                $(".tabfil3").removeClass("newclassfortab3");
                $(".tabfil4").removeClass("newclassfortab4");
                $('.flight_filter_tag.filtertag > .fft_right').removeClass('fft_right_collapsed');
                $('.flight_filter_tag.sorttag > .fft_right').removeClass('fft_right_collapsed');
                $('.flight_filter_tag.sorttag1 > .fft_right').removeClass('fft_right_collapsed');
            }
            if ($(".tabsformobile1").hasClass("newclassfortab1a")) {
                $('.newcontainer1').show();
                $(".hidefilter_icon").show();
                $(".hidden_icon_for_color").removeClass("Bottom_green_my");
                $(".hidden_icon_for_color").removeClass("Bottom_yellow_my");
                $(".hidden_icon_for_color").removeClass(" Bottom_pink_my");
                $(".hidden_icon_for_color").addClass("Bottom_blue_my");
                $(".tabfil1").addClass("newclassfortab1");
                $(".tabfil2").removeClass("newclassfortab2");
                $(".tabfil3").removeClass("newclassfortab3");
                $(".tabfil4").removeClass("newclassfortab4");
            }
            else if ($(".tabsformobile2").hasClass("newclassfortab2a")) {
                $('.newcontainer2').show();
                $(".hidefilter_icon").show();
                $(".hidden_icon_for_color").removeClass("Bottom_blue_my");
                $(".hidden_icon_for_color").removeClass("Bottom_yellow_my");
                $(".hidden_icon_for_color").removeClass(" Bottom_pink_my");
                $(".hidden_icon_for_color").addClass("Bottom_green_my");
                $(".tabfil1").removeClass("newclassfortab1");
                $(".tabfil2").addClass("newclassfortab2");
                $(".tabfil3").removeClass("newclassfortab3");
                $(".tabfil4").removeClass("newclassfortab4");
            }
            else if ($(".tabsformobile3").hasClass("newclassfortab3a")) {
                $('.newcontainer3').show();
                $(".hidefilter_icon").show();
                $(".hidden_icon_for_color").removeClass("Bottom_blue_my");
                $(".hidden_icon_for_color").removeClass("Bottom_yellow_my");
                $(".hidden_icon_for_color").removeClass("Bottom_green_my");
                $(".hidden_icon_for_color").addClass(" Bottom_pink_my");
                $(".tabfil1").removeClass("newclassfortab1");
                $(".tabfil2").removeClass("newclassfortab2");
                $(".tabfil3").addClass("newclassfortab3");
                $(".tabfil4").removeClass("newclassfortab4");
            }
            else if ($(".tabsformobile4").hasClass("newclassfortab4a")) {
                $('.newcontainer4').show();
                $(".hidefilter_icon").show();
                $(".hidden_icon_for_color").removeClass("Bottom_green_my");
                $(".hidden_icon_for_color").removeClass("Bottom_blue_my");
                $(".hidden_icon_for_color").removeClass(" Bottom_pink_my");
                $(".hidden_icon_for_color").addClass("Bottom_yellow_my");
                $(".tabfil1").removeClass("newclassfortab1");
                $(".tabfil2").removeClass("newclassfortab2");
                $(".tabfil3").removeClass("newclassfortab3");
                $(".tabfil4").addClass("newclassfortab4");
            }
        }
        else if(resizeReal && $(window).width() > 900) {
        if (!$(".s2_fil_in_left").is(':visible')) {
            $(".s2_fil_in_left").show();
        }
        $(".s2_fil_in_right_sm").hide();
        if (!$.trim($(".s2_filter_inner .s2_fil_in_right").html()).length) {
            var section = $('.s2_fil_in_right_sm').children().detach();
            section.appendTo(".s2_fil_in_right");
            $('.s2_fil_in_right_sm').hide();
        }
        $('.s2filter_cc_inner').parent().hide();
        $('.flight_group_container').find(".s2_mcb_ml_left_container").show();
        $('.flight_group_container').find(".s2_mcb_ml_right_container").show();
        $('.flight_group_container').each(function (index, element) {
            $('.showmoredata.' + element.id).hide();
        });

        $(".tlla_right").show();
        $(".dekstop_menu").show();

        $(".tapto_search").css('display', 'none');
        $(".taptosearch_tick").css('display', 'none');
        $(".sir_tabresult").css('display', 'block');
        } else if (resizeReal && $(window).width() < 900) {
        // mobile layout start here
        if (!$.trim($(".s2_fil_in_right_sm").html()).length && $('.s2_fil_in_right_sm').is(':visible')) {
            var section = $('.s2_fil_in_right').children().detach();
            section.appendTo(".s2_fil_in_right_sm");
        }
        $('.s2filter_cc_inner').parent().hide();

        //$('.flight_group_container').find(".s2_mcb_ml_left_container").hide();
        //$('.flight_group_container').find(".s2_mcb_ml_right_container").hide();
        $('.flight_group_container').each(function (index, element) {
            $('.showmoredata.' + element.id).show();
        });
    }
});

$(function () {
    $(".close_tabn").click(function () {
        $(".show_more_flight_icon").removeClass("smfi_imp");
        $(".show_more_flight_icon").css('background-image', 'url("../images/more_info_icon_down.png")');
        $(".mf_content_container").slideUp();
        $(".show_more_flight_icon").text("Show more booking details");
        $(".moreflight_box").removeClass("mfb_gray_bg");
    })
    /*Open frequent traveller start*/
    $(".openff").click(function () {
        $(".fullcover_div2").fadeIn();
    });
    $(".addff").click(function () {
        $(".fullcover_div3").fadeIn();
    });

    /*Form error message start*/
    $(".error_form_imp_name").next().after("<div class='textforerrormsg'>Please fill in your name</div>");
    $(".error_form_imp_email").after("<div class='textforerrormsg'>Please fill in your email</div>");
    $(".error_form_imp_number").after("<div class='textforerrormsg'>Please provide your primary number</div>");
    $(".error_form_imp_password").after("<div class='textforerrormsg'>Your old password does not match</div>");
    /*Form error message start*/
    /*membership tab start*/
    $(".rpc_redtabs").click(function () {
        $(this).next().slideToggle();
        $(this).toggleClass("rpc_minustab");
    });
    /*membership tab end*/
    
    //$(document).on('click', '.farerules', function (e) {
    //    $(".fullcover_div1").fadeIn();
    //});

    $(document).on('click', '.filtertag', function (e) {
        //$(".s2_fil_in_left").slideToggle();
        $(".tabsformobile1").slideToggle();
        $(".tabsformobile2").slideToggle();
        $(".tabsformobile3").slideToggle();
        $(".tabsformobile4").slideToggle();
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
        $('.hidefilter_icon').hide();
        if ($(this).find('.fft_right').hasClass('fft_right_collapsed')) {
            $(this).find('.fft_right').removeClass('fft_right_collapsed');
        } else {
            $(this).find('.fft_right').addClass('fft_right_collapsed');
        }
        hidefilter();
        //$(".hidefilter_icon").hide();
    });

    $('#result-content').on('click', '.outbound_click', function (e) {
        if ($(window).width() <= 900) {
            $(this).parents('.flight_group_container').find(".s2_mcb_ml_left_container").slideToggle();
            if ($(this).hasClass("boundarrow")) {
                $(this).removeClass("boundarrow");
            } else {
                $(this).addClass("boundarrow");
            }
        }
    });

    $('#result-content').on('click', '.inbound_click', function (e) {
        if ($(window).width() <= 900) {
            var grpSelector = $(this).parents('.flight_group_container').find('.s2_mcb_ml_right_container');
            var isDisplayed = grpSelector.is(':visible');
            grpSelector.slideToggle();
            if ($(this).hasClass("boundarrow")) {
                $(this).removeClass("boundarrow");
            } else {
                $(this).addClass("boundarrow");
            }
            if (isDisplayed) {
                $('html, body').animate({
                    scrollTop: $(this).parent().prev().offset().top
                }, 300);
            }
        }
    });

    //$(document).on('click', '.sorttag', function (e) {
    //    if (!$.trim($(".s2_fil_in_right_sm").html()).length) {
    //        var section = $('.s2_fil_in_right').children().detach();
    //        section.appendTo(".s2_fil_in_right_sm");
    //    } else {
    //        var section = $('.s2_fil_in_right_sm').children().detach();
    //        section.appendTo(".s2_fil_in_right");
    //    }
    //});

    $(document).on('click', '.s2_fil_in_right_sm.bestdeal', function (e) {
        $('#sort-option').val("PriceAsc");
        filterAirline();
    });
    $(document).on('click', '.s2_fil_in_right_sm.fastroute', function (e) {
        $('#sort-option').val("DepartureTimeAsc");
        filterAirline();
    });
    $(document).on('click', '.s2_fil_in_right_sm.shortduration', function (e) {
        $('#sort-option').val("DurationAsc");
        filterAirline();
    });
    $(document).on('click', '.flight_filter_tag.sorttag', function (e) {
        $('.s2_fil_in_right_sm').slideToggle();
        if ($(this).find('.fft_right').hasClass('fft_right_collapsed')) {
            $(this).find('.fft_right').removeClass('fft_right_collapsed');
        } else {
            $(this).find('.fft_right').addClass('fft_right_collapsed');
        }
    })
    $(".payment_gray_tabs").click(function () {
        //$(this).next().slideToggle();
        // Twin - 2016/12/04, fix cannot show all passenger
        $(this).parents('.pasengerTypeInfo').find('.payment_fulldetail_container').slideToggle();
        $(this).toggleClass("payment_tab_active");
        $(this).find(".pgt_right").toggleClass("pgt_right_active");
    });

    $(".show_more_flight_icon_p").click(function () {
        $(".spcldiv").toggleClass("paydisp_none");
        if ($(".show_more_flight_icon_p").text() == "Show more flights details") {
            $(".show_more_flight_icon_p").text("Hide flight details");
            $(".show_more_flight_icon_p").css('background-image', 'url("../images/more_info_icon.png")');
        } else {
            $(".show_more_flight_icon_p").text("Show more flights details");
            $(".show_more_flight_icon_p").css('background-image', 'url("../images/more_info_icon_down.png")');
        }
    });
    $(".s3-1_form_button").click(function () {
        // 2016/12/16, Disabled for prevent popout
        //$(".fullcover_div").fadeIn();
    });
    $(".loginclose img").click(function () {
        $(".fullcover_div").fadeOut();
        $("body").removeClass("modal-open");
        /* 20170108 - caused header issues */
        //$(".tlla_left").removeClass("posinherit");
        //$(".tlla_left").addClass("posabs");
        $(".fullcover_div1").fadeOut();
        $(".fullcover_div2").fadeOut();
        $(".fullcover_div3").fadeOut();
        $(".fullcover_div4").fadeOut();
        $(".fullcover_div5").fadeOut();
        $(".fullcover_div6").fadeOut();

    });
    $(".s3-1_flyer_left_dupinput").click(function () {
        //$(".ffn_inputbox ul").toggle();
        var selector = $(this).data('ffair');
        $("#" + selector).toggle();
        $(this).parent().find(".ffn_inputboxulli_container ul").toggle();
    });
    /*flight page more detail start*/
    $(document).on('click', '.s2_mcbmlid_dc_right', function (e) {
        $(this).parent().prev().slideToggle("slow");
        $(this).find(".showdetails").toggleClass("hidedetail");
        if ($(this).find(".showdetails").text() == "Show Details") {
            var position = parseInt($(this).parents('.flight_group_container').data('gapo'));
            var stop = $(this).data('stop');
            var direction = $(this).data('direction');
            ecomdetail((isNaN(position) ? 0 : position), stop, direction);
            $(this).find(".showdetails").text("Hide Details");
        } else {
            $(this).find(".showdetails").text("Show Details");
            smoothScroll($(this).parents('.s2_mcb_ml_left'));
        }
    });
    /*flight page more detail end*/

    /*filter start*/
    $('.flight-filter-panel').on('click', '.tabsformobile1', function () {
        $(".newcontainer2").slideUp();
        $(".newcontainer4").slideUp();
        $(".newcontainer3").slideUp();
        $(".newcontainer1").slideToggle();
        $(this).toggleClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile3").removeClass("newclassfortab3a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
        $(".tabfil1").removeClass("newclassfortab1");
    });
    $('.flight-filter-panel').on('click', '.tabsformobile2', function () {
        $(".newcontainer1").slideUp();
        $(".newcontainer4").slideUp();
        $(".newcontainer3").slideUp();
        $(".newcontainer2").slideToggle();
        $(this).toggleClass("newclassfortab2a");
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile3").removeClass("newclassfortab3a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
        $(".tabfil2").removeClass("newclassfortab2");
    });
    $('.flight-filter-panel').on('click', '.tabsformobile3', function () {
        $(".newcontainer1").slideUp();
        $(".newcontainer2").slideUp();
        $(".newcontainer4").slideUp();
        $(".newcontainer3").slideToggle();
        $(this).toggleClass("newclassfortab3a");
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
        $(".tabfil3").removeClass("newclassfortab3");
    })
    $('.flight-filter-panel').on('click', '.tabsformobile4', function () {
        $(".newcontainer1").slideUp();
        $(".newcontainer2").slideUp();
        $(".newcontainer3").slideUp();
        $(".newcontainer4").slideToggle();
        $(this).toggleClass("newclassfortab4a");
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile3").removeClass("newclassfortab3a");
        $(".tabfil4").removeClass("newclassfortab4");
    })
    $('.flight-filter-panel').on('click', '.tabfil1', function () {
        $(".newcontainer2").slideUp();
        $(".newcontainer4").slideUp();
        $(".newcontainer3").slideUp();
        $(".newcontainer1").slideDown();
        $(".tabfil2").removeClass("newclassfortab2");
        $(".tabfil3").removeClass("newclassfortab3");
        $(".tabfil4").removeClass("newclassfortab4");
        $(this).addClass("newclassfortab1");
        $(".hidden_icon_for_color").removeClass("Bottom_green_my");
        $(".hidden_icon_for_color").removeClass("Bottom_pink_my");
        $(".hidden_icon_for_color").removeClass("Bottom_yellow_my");
        $(".hidden_icon_for_color").addClass("Bottom_blue_my");
        $(".tabsformobile1").addClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile3").removeClass("newclassfortab3a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
    })
    $('.flight-filter-panel').on('click', '.tabfil2', function () {
        $(".newcontainer1").slideUp();
        $(".newcontainer3").slideUp();
        $(".newcontainer4").slideUp();
        $(".newcontainer2").slideDown();
        $(".tabfil1").removeClass("newclassfortab1");
        $(".tabfil3").removeClass("newclassfortab3");
        $(".tabfil4").removeClass("newclassfortab4");
        $(this).addClass("newclassfortab2");
        $(".hidden_icon_for_color").removeClass("Bottom_blue_my");
        $(".hidden_icon_for_color").removeClass("Bottom_pink_my");
        $(".hidden_icon_for_color").removeClass("Bottom_yellow_my");
        $(".hidden_icon_for_color").addClass("Bottom_green_my");
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile2").addClass("newclassfortab2a");
        $(".tabsformobile3").removeClass("newclassfortab3a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
    })
    $('.flight-filter-panel').on('click', '.tabfil3', function () {
        $(".newcontainer1").slideUp();
        $(".newcontainer2").slideUp();
        $(".newcontainer4").slideUp();
        $(".newcontainer3").slideDown();
        $(".tabfil1").removeClass("newclassfortab1");
        $(".tabfil2").removeClass("newclassfortab2");
        $(".tabfil4").removeClass("newclassfortab4");
        $(this).addClass("newclassfortab3");
        $(".hidden_icon_for_color").removeClass("Bottom_blue_my");
        $(".hidden_icon_for_color").removeClass("Bottom_green_my");
        $(".hidden_icon_for_color").removeClass("Bottom_yellow_my");
        $(".hidden_icon_for_color").addClass("Bottom_pink_my");
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile3").addClass("newclassfortab3a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
    })
    $('.flight-filter-panel').on('click', '.tabfil4', function () {
        $(".newcontainer1").slideUp();
        $(".newcontainer2").slideUp();
        $(".newcontainer3").slideUp();
        $(".newcontainer4").slideDown();
        $(".tabfil1").removeClass("newclassfortab1");
        $(".tabfil2").removeClass("newclassfortab2");
        $(".tabfil3").removeClass("newclassfortab3");
        $(this).addClass("newclassfortab4");
        $(".hidden_icon_for_color").removeClass("Bottom_blue_my");
        $(".hidden_icon_for_color").removeClass("Bottom_green_my");
        $(".hidden_icon_for_color").removeClass("Bottom_pink_my");
        $(".hidden_icon_for_color").addClass("Bottom_yellow_my");
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile3").removeClass("newclassfortab3a");
        $(".tabsformobile4").addClass("newclassfortab4a");
    })

    $('.flight-filter-panel').on('click', '.s2_fil_inl_filboxes', function (e) {
        if (!$(".hidefilter_icon").is(':visible')) {
            $(".hidefilter_icon").fadeIn();
        }
    });
    $('.flight-filter-panel').on('click', '.hidefilter_icon', function (e) {
        hidefilter(); 
        $('.flight_filter_tag.filtertag > .fft_right').removeClass('fft_right_collapsed');
    })

    function hidefilter() {
        $(".newcontainer1").slideUp();
        $(".newcontainer2").slideUp();
        $(".newcontainer3").slideUp();
        $(".newcontainer4").slideUp();
        $(".tabfil1").removeClass("newclassfortab1");
        $(".tabfil2").removeClass("newclassfortab2");
        $(".tabfil3").removeClass("newclassfortab3");
        $(".tabfil4").removeClass("newclassfortab4");
        $(".tabsformobile1").removeClass("newclassfortab1a");
        $(".tabsformobile2").removeClass("newclassfortab2a");
        $(".tabsformobile3").removeClass("newclassfortab3a");
        $(".tabsformobile4").removeClass("newclassfortab4a");
        $('.hidefilter_icon').hide();
    }

    function reInitializeFilterPanel() {
        hidefilter();
        $(".tabsformobile1").hide();
        $(".tabsformobile2").hide();
        $(".tabsformobile3").hide();
        $(".tabsformobile4").hide();
        $('#filter-airlines').parents('.s2filter_cc_inner').parent().hide();
    }

    /*filter end*/
    $(".ffn_inputboxulli_container ul li").click(function () {
        var tab_id = $(this).attr('inpvalue');
        var imgpath = $(this).find(".ffn_ulli_left").html();
        $(this).parent().parent().parent().find(".s3-1_flyer_left_dupinput").html(imgpath);
        $(this).parent().parent().parent().find(".frquentflyer").val(tab_id);
        $(".ffn_inputboxulli_container ul").hide();
    });
    $(".show_more_flight_icon").click(function () {
        $(this).toggleClass("smfi_imp");
        $(".moreflight_box").toggleClass("mfb_gray_bg");
        $(".mf_content_container").slideToggle();
        if ($(".show_more_flight_icon").text() == "Show more booking details") {
            $(".show_more_flight_icon").text("Hide booking details");
            $(".show_more_flight_icon").css('background-image', 'url("../images/more_info_icon.png")');
        } else {
            $(".show_more_flight_icon").text("Show more booking details");
            $(".show_more_flight_icon").css('background-image', 'url("../images/more_info_icon_down.png")');
        }
    });

    $(document).on('click', '.show_more_flight_icon1', function (e) {
        var selector = $(this);
        var css = $(this).attr('id');
        $("." + css).toggle();
        if (selector.text() == "Show more flight option") {
            selector.text("Hide flight option");
            $(this).css('background-image', 'url("../images/more_info_icon.png")');
        } else {
            selector.text("Show more flight option");
            $(this).css('background-image', 'url("../images/more_info_icon_down.png")');
            $('html, body').animate({
                scrollTop: $(this).parents('.flight_group_container').offset().top
            }, 300);
        }
    });

    $(".contact_detail_heading_small").click(function () {
        var focusElement = function (element) {
            $('html, body').animate({
                scrollTop: $(element).offset().top
            }, 300);
        };

        var tab_id = $(this).attr('value');
        switchTab(tab_id);

        var divFocus = $("#" + tab_id).parent();
        focusElement(divFocus);
    });

    $(".s3-1_guestdetail_box ul li").click(function () {
        var tab_id = $(this).attr('value');
        switchTab(tab_id);
    });

    var switchTab = function (tab_id) {
        $(this).attr('aria-expanded', true);

        $(".contact_detail_heading_small[value!=" + tab_id + "]").attr("aria-expanded", false).find(".chhc_bar").removeClass("cdhc_redbar");
        $(".contact_detail_heading_small[value!=" + tab_id + "]").find(".chhc_bar").removeClass("cdhc_redbar");
        $(".booking-form").not("#" + tab_id).hide();

        $("#" + tab_id).fadeIn();
        $(".contact_detail_heading_small[value=" + tab_id + "]").find(".chhc_bar").addClass("cdhc_redbar");

        $(".s3-1_guestdetail_box ul li[value!=" + tab_id + "]").removeClass("s3-1_activli");
        $(".s3-1_guestdetail_box ul li[value=" + tab_id + "]").addClass("s3-1_activli");
    }

    $(".searchtab1").click(function () {
        $("#search_hotel").fadeIn();
        $("#search_flight").hide();
        $("#search_package").hide();
        $("#search_car").hide();
        $(this).addClass("active_tab");
        $("#IsDynamic").val(false);
        $("#IsFixedPrice").val(false);
        $(".dynamic_room").hide();
        $(".fixedprice").hide();
        $(".searchtab2").removeClass("active_tab");
        $(".searchtab3").removeClass("active_tab");
        $(".searchtab4").removeClass("active_tab");
        $(".searchtab5").removeClass("active_tab");
    });
    $(".searchtab2").click(function () {
        $("#search_hotel").hide();
        $("#search_flight").fadeIn();
        $("#search_package").hide();
        $("#search_car").hide();
        $(".tcc_triptypediv").show();
        $("#IsDynamic").val(false);
        $("#IsFixedPrice").val(false);
        $(".dynamic_room").hide();
        $(".fixedprice").hide();
        $(".tcc_threetabs2a").attr("style", "margin-left: 20%;");
        $(this).addClass("active_tab");
        $(".searchtab1").removeClass("active_tab");
        $(".searchtab3").removeClass("active_tab");
        $(".searchtab4").removeClass("active_tab");
        $(".searchtab5").removeClass("active_tab");
    });
    $(".searchtab3").click(function () {
        $("#search_hotel").hide();
        $("#search_flight").fadeIn();
        $("#search_package").hide();
        $("#search_car").hide();
        $(".tcc_triptypediv").hide();
        $("#IsDynamic").val(true);
        $("#IsFixedPrice").val(false);
        $(".dynamic_room").show();
        $(".fixedprice").hide();
        $(".tcc_threetabs2a").attr("style", "margin-left: 35%;");
        $(this).addClass("active_tab");
        $(".searchtab2").removeClass("active_tab");
        $(".searchtab1").removeClass("active_tab");
        $(".searchtab4").removeClass("active_tab");
        $(".searchtab5").removeClass("active_tab");
    });

    $(".searchtab4").click(function () {
        $("#search_hotel").hide();
        $("#search_flight").fadeIn();
        $("#search_package").hide();
        $("#search_car").hide();
        $(".tcc_triptypediv").hide();
        $("#IsDynamic").val(true);
        $("#IsFixedPrice").val(true);
        $(".dynamic_room").show();
        $(".fixedprice").show();
        $(".tcc_threetabs2a").attr("style", "margin-left: 35%;");
        $(this).addClass("active_tab");
        $(".searchtab1").removeClass("active_tab");
        $(".searchtab2").removeClass("active_tab");
        $(".searchtab3").removeClass("active_tab");
        $(".searchtab5").removeClass("active_tab");
    });

    $(".searchtab5").click(function () {
        $("#search_hotel").hide();
        $("#search_flight").hide();
        $("#search_package").hide();
        $("#search_car").show();
        $(".tcc_triptypediv").hide();
        $("#IsDynamic").val(false);
        $("#IsFixedPrice").val(false);
        $(".dynamic_room").hide();
        $(".fixedprice").hide();
        $(".tcc_threetabs2a").attr("style", "margin-left: 35%;");
        $(this).addClass("active_tab");
        $(".searchtab1").removeClass("active_tab");
        $(".searchtab2").removeClass("active_tab");
        $(".searchtab3").removeClass("active_tab");
        $(".searchtab4").removeClass("active_tab");
    });

    $(".tcct1").click(function () {
        $(this).addClass("return_icon_active");
        $(".tcct2").removeClass("oneway_icon_active");
        $(".tcct3").removeClass("multi_icon_active");
        $('.flight_twoway').show();
        $('#oneway-clear').remove();
    });
    $(".tcct2").click(function () {
        $(this).addClass("oneway_icon_active");
        $(".tcct1").removeClass("return_icon_active");
        $(".tcct3").removeClass("multi_icon_active");
        $('.flight_twoway').hide();
        if ($('#oneway-clear').length == 0) {
            $('.flight_oneway').append("<div id='oneway-clear' class='clear'></div>");
        }
    });
    $(".tcct3").click(function () {
        $(this).addClass("multi_icon_active");
        $(".tcct2").removeClass("oneway_icon_active");
        $(".tcct1").removeClass("return_icon_active");
    });

    $(".mobile_menu_button img").click(function () {
        $(".dekstop_menu").slideToggle();
        $(".gray_toll_tip").removeClass("hidden");
    });

    $(".tapto_search").click(function () {
        $(this).slideUp();
        $(".taptosearch_tick").slideUp();
        $(".sir_tabresult").slideDown();
    });
    $(".searchclose img").click(function () {
        $(".tapto_search").slideDown();
        $(".taptosearch_tick").slideDown();
        $(".sir_tabresult").slideUp();
    });

    $('div#menu_name').click(function () {
        if ($('.gray_toll_tip').hasClass('hidden')) {
            $(".gray_toll_tip").removeClass("hidden");
        } else {
            $(".gray_toll_tip").addClass("hidden");
        }
    })

    $(document).mouseup(function (e) {
        var container = $("#menu_name, .mobile_menu_button img, .gray_toll_tip");
        if (!container.is(e.target) // if the target of the click isn't the container...
           && container.has(e.target).length === 0) // ... nor a descendant of the container
        {
            if ($(window).width() < 900) {
                $(".dekstop_menu").css('display', 'none');
            } else {
                container.parent().find(".gray_toll_tip").addClass("hidden");
            }
        }
    });

    $(".ffc2_r2_b5 img").click(function () {
        if (!$(".yellow_toll_tip").css('display', 'block')) {
            $(".yellow_toll_tip").fadeIn();
        }
    });

    $(document).mouseup(function (e) {
        var container = $(".yellow_toll_tip");
        if (!container.is(e.target) && container.has(e.target).length === 0) {
            $(".yellow_toll_tip").fadeOut();
        }
    });

    $(".s3-1_fi_row1-right").mouseover(function () {
        $(this).find(".s3-1_form_toll_tip").show();
    });

    $(".s3-1_fi_row1-right").mouseout(function () {
        $(this).find(".s3-1_form_toll_tip").hide();
    });

    /* 2017/01/18 - Hotel tab event start */
    $(document).on("click", function () {
        $(".yellow_toll_tip1").hide();
    });

    $(".tooltip1").click(function (event) {
        $(".yellow_toll_tip").fadeIn();
        $(".yellow_toll_tip").delay(2000).fadeOut();
    })
    $(".tooltip2").click(function (event) {
        event.stopPropagation();
        $(".yellow_toll_tip1").fadeIn();
    })
    $(".tooltip2").mouseover(function (event) {
        $(".yellow_toll_tip1").fadeIn();
        $(".yellow_toll_tip").delay(2000).fadeOut();
    })
    $(".tooltip2").mouseout(function (event) {
        $(".yellow_toll_tip1").fadeOut();
    })

    //$(".yellow_toll_tip1").click(function (event) {
    //    event.stopPropagation();
    //});
    /* 2017/01/18 - Hotel tab event end */
});
