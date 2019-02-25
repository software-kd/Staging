var $star_rating = $('.star-rating .fa');

var SetRatingStar = function() {
  return $star_rating.each(function() {
    if (parseInt($star_rating.siblings('input.rating-value').val()) >= parseInt($(this).data('rating'))) {
      return $(this).removeClass('fa-star-o').addClass('fa-star');
    } else {
      return $(this).removeClass('fa-star').addClass('fa-star-o');
    }
  });
};

$star_rating.on('click', function() {
    $star_rating.siblings('input.rating-value').val($(this).data('rating'));
    var _star = $("[name='Rating']").val();
    var data = {
        Rating: _star,
    };
    getCList(1, data);
  return SetRatingStar();
});

SetRatingStar();
$(document).ready(function() {
    $(".searchcar").on('click', function () {
        var _name = $("[name='carname']").val();
        var data = {
            CarModel: _name,
        };
        getCList(1, data);
    });
    $("[name='sortby']").on('change', function () {
        var _sort = $(this).val();
        var data = {
            SortBy: _sort,
        };
        getCList(1, data);
    });
});

$(document).ready(function(){
	$(".carousel").swipe( {
		swipeLeft: function() {
			$(this).carousel("next");
		},
		swipeRight: function() {
			$(this).carousel("prev");
		},
		allowPageScroll: "vertical"
	});
});

   function getCList (pg, data) {
        var q = (location.search.indexOf('?') === -1 ? '?version=v2' : '&version=v2')

        if (typeof (pg) !== 'undefined')
            q += '&page=' + pg;

        $.ajax({
            type: "POST",
            cache: false,
            dataType: 'html',
            data: typeof (data) !== 'undefined' ? data : $('form').serialize(),
            url: '/CarRental/GetCarRentalList' + location.search + q,
            timeout: 120000, // 2 minutes.
            beforeSend: function (data) {
                loadModal(true);
            },
        }).then(function (res) {
            $('.car-results').html(res);
        }).done(function (res) {
            loadModal(false);
        });
    }

    if ($('.car-results').length === 1 && $('.car-results').html().trim().length === 0) {
        getCList();
    }

    $('.car-results').on('click', '[data-page-no]', function (e) {
        var pg = $(this).data('page-no');
        getCList(pg);
    });
    $(".car-results").on('click', '.reservebtn', function (e) {
        var ele = $(this)
        submitC(ele);
    });

    var pushCarToCart = function (e) {
        var rd = {
            BranchID: $(e).data('data-branchid'),
            VehicleID: $(e).attr("data-vehicleid"),
        };
        return rd;
    }

    var submitC = function (element) {
        var cd = {
            BranchID: $(element).data('data-branchid'),
            VehicleID: $(element).attr("data-vehicleid"),
        };

        var url = $('input[type="hidden"].submit').data('url');
        $('<form action="' + url + '" method="POST">' +
            '<input hidden="key" name="key" value="' + encodeURIComponent(JSON.stringify(cd)) + '" />' +
            '</form>')
            .appendTo($(document.body))
            .submit();
    }

$(document).ready(function(){

    function changeval(input, val) {
      var adult = parseFloat($('#ad').val());
      var child = parseFloat($('#ch').val());
      var infant = parseFloat($('#in').val());
      var totalpsg = adult + infant + child;
      var value = parseFloat($('#' + input).val());

      if ((val == "-") && value > 0 && totalpsg <= 6) {
          value--;
      } else if ((val == "+") && totalpsg < 6 && value >= 0)
          value++;

      $('#' + input).val(value);

    }

    $('#show-form').click(function () {
        $('.my-form').removeClass('hide');
        $( "#name" ).focus();
    });
    $('#cancel').click(function () {
        $('.my-form').addClass('hide');
    });

});