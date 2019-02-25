$(function () {
    $('[data-toggle="tooltip"]').tooltip(
        {
            template: '<div class="tooltip" role="tooltip"><div class="arrow"></div><div class="tooltip-inner"></div></div>'
        });

    $('[data-toggle="popover"]').popover({
        trigger: 'focus'
    });
})

var loadModal = function (a) {
    var c = $('#loadingModal');
    if (a) {
        c.modal({
            keyboard: false,
            backdrop: 'static',
            show: true
        })
    }
    else {
        c.modal('hide');
    }
}

var loadImg = function () {
    return '<div class="text-center">'
        + '<img class src="../../Images/txt_load.gif" />'
        + '</div>';
}

var dynamicModal = function (title, content, showConfrim, islgSize) {
    var modal = $('#genericModal');
    pushModal(modal, title, content, showConfrim);

    if (typeof (islgSize) === 'undefined' || islgSize === 'false') {
        modal.find('.modal-dialog').removeClass('modal-lg');
    }
    else if (typeof (islgSize) !== 'undefined' || islgSize === 'true') {
        modal.find('.modal-dialog').addClass('modal-lg');
    }
    return modal;
}

var pushModal = function (modal, title, content, showConfirm) {
    modal.find('.modal-title').text(title);
    modal.find('.modal-body').html(content);

    if (typeof (showConfirm) !== 'undefined' && typeof (showConfirm) === 'boolean'
        && !showConfirm) {
        modal.find('.modal-confirm').hide();
    }
    else {
        modal.find('.modal-confirm').show();
    }
}

var stEle = function (el) {
    $('html, body').animate({
        scrollTop: $(el).offset().top - 50
    }, 500);
}

$('.resbar-htlist-btn').on('click', 'a', function (e) {
    window.location = '/hotel/search' + location.search;
});

$('.resbar-fltlist-btn').on('click', 'a', function (e) {
    window.location = '/flight/search' + location.search;
});