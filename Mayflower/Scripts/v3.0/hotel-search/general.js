//Room/adult/kids counter for top filter
$('.count-plus').on('click', function (e) {
    e.preventDefault();

    var getType = $(this).attr('data-type');
    var getVal = $('li span[data-type="' + getType + '"]').html();
    var lbl = $(this).attr('data-label');
    var maxVal = $(this).attr('data-max');
    maxVal = parseInt(maxVal) !== 'NaN' ? parseInt(maxVal) : maxVal;

    if (getVal < maxVal) {
        getVal++;

        $('li span[data-type="' + getType + '"]').html(getVal);

        if (getVal > 1) {
            var countText = getVal + " " + lbl + "s";
        } else {
            var countText = getVal + " " + lbl;
        }
        $('.drop-trigger span[data-type="' + getType + '"]').html(countText);
        $('.drop-trigger input[data-type="' + getType + '"]').val(getVal);


    } else {
        $('li span[data-type="' + getType + '"]').html(maxVal);

        if (getVal > 1) {
            var countText = maxVal + " " + lbl + "s";

        } else {
            var countText = maxVal + " " + lbl;
        }
        $('.drop-trigger span[data-type="' + getType + '"]').html(countText);
        $('.drop-trigger input[data-type="' + getType + '"]').val(maxVal);
    }


});

$('.count-minus').on('click', function (e) {
    e.preventDefault();

    var getType = $(this).attr('data-type');
    var getVal = $('li span[data-type="' + getType + '"]').html();
    var lbl = $(this).attr('data-label');
    var minVal = $(this).attr('data-min');
    minVal = parseInt(minVal) !== 'NaN' ? parseInt(minVal) : minVal;

    if (getVal > minVal) {
        getVal--;
        if (getVal > 1) {
            var countText = getVal + " " + lbl + "s";
        } else {
            var countText = getVal + " " + lbl;
        }
        $('.drop-trigger span[data-type="' + getType + '"]').html(countText);
        $('.drop-trigger input[data-type="' + getType + '"]').val(getVal);

        $('li span[data-type="' + getType + '"]').html(getVal);

    } else {
        $('li span[data-type="' + getType + '"]').html(minVal);
        if (minVal > 1) {
            var countText = minVal + " " + lbl + "s";

        } else {
            var countText = minVal + " " + lbl;
        }
        $('.drop-trigger span[data-type="' + getType + '"]').html(countText);
        $('.drop-trigger input[data-type="' + getType + '"]').val(minVal);
    }


});

//Custom dropdown
$('.drop-trigger').on('click', function (e) {
    e.preventDefault();

    if ($(this).hasClass('open')) {
        $(this).removeClass('open');

    } else {
        $(this).addClass('open');
    }

    $(this).parent('.custom-drop').find('ul').toggle();

});

$('a[data-rating2]').on('click', function (e) {
    e.preventDefault();
    var getVal = $(this).attr('data-rating2');

    $('.mob-rating2 input').val(getVal);
    $('a[data-rating2]').addClass('disabled');
    for (var i = 0; i < getVal; i++) {
        $('a[data-rating2]').eq(i).removeClass('disabled');
    }

});