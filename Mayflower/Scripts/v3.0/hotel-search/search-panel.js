var datePicker = function (el, minDate, maxDate, minYear, maxYear, maxDayRange, startDate, endDate) {
    var dfFrom = $(el).siblings('.dtFrom');
    var dfTo = $(el).siblings('.dtTo');

    $(el).daterangepicker({
        "autoApply": true,
        "showDropdowns": true,
        "locale": {
            "format": "DD MMM YYYY",
            "separator": " - ",
        },
        "minDate": moment(minDate, 'MM/DD/YYYY'),
        "maxDate": moment(maxDate, 'MM/DD/YYYY'),
        "minYear": minYear,
        "maxYear": maxYear,
        "maxSpan": {
            "days": maxDayRange
        },
        "startDate": typeof (dfFrom) !== 'undefined' ? moment(dfFrom.val(), 'D-MMM-YYYY') : moment(startDate, 'MM/DD/YYYY'),
        "endDate": typeof (dfTo) !== 'undefined' ? moment(dfTo.val(), 'D-MMM-YYYY') : moment(endDate, 'MM/DD/YYYY'),
    }, function (start, end) {
        //console.log('New date range selected: ' + start.format('DD MMM YYYY') + ' to ' + end.format('DD MMM YYYY'));
    });
}

$(function () {
    $('input[data-type="dtPicker"]').on('apply.daterangepicker', function (ev, picker) {
        $(this).siblings('.dtFrom').val(picker.startDate.format('DD-MMM-YYYY'));
        $(this).siblings('.dtTo').val(picker.endDate.format('DD-MMM-YYYY'));
        //$(this).val(picker.startDate.format('DD MMM YYYY') + ' - ' + picker.endDate.format('DD MMM YYYY'));
    });
});