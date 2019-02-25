$('.dt-hotel.ht-dest').typeahead({
    dynamic: true,
    //mustSelectItem: true,
    blurOnTab: false,
    minLength: 1,
    maxItem: 20,
    order: "asc",
    template: "{{display}} <small style='color:#999;'>{{group}}</small>",
    emptyTemplate: function (query) {
        if (query.length > 0) {
            return 'No results found for "' + query + '"';
        }
    },
    source: {
        City: {
            ajax: function (query) {
                return {
                    type: "POST",
                    url: "/hotel/getdestination_v2",
                    //dataType: "json",
                    path: "data.City",
                    data: {
                        data: query
                    }
                }
            }
        },
        Country: {
            compression: true,
            cache: true,
            ajax: function (query) {
                return {
                    type: "POST",
                    url: "/hotel/gethotel_country",
                    //dataType: "json",
                    path: "data.Country",
                    data: {
                        data: query
                    }
                }
            }
        },
    },
    callback: {
        onNavigateAfter: function (node, lis, a, item, query, event) {
            if (~[38, 40].indexOf(event.keyCode)) {
                var resultList = node.closest("form").find("ul.typeahead__list"),
                    activeLi = lis.filter("li.active"),
                    offsetTop = activeLi[0] && activeLi[0].offsetTop - (resultList.height() / 2) || 0;

                resultList.scrollTop(offsetTop);
            }

        },
        onClickAfter: function (node, a, item, event) {
            event.preventDefault();
        },
        onResult: function (node, query, result, resultCount) {
            if (query === "") return;

            var text = "";
            if (result.length > 0 && result.length < resultCount) {
                text = "Showing <strong>" + result.length + "</strong> of <strong>" + resultCount + '</strong> elements matching "' + query + '"';
            } else if (result.length > 0) {
                text = 'Showing <strong>' + result.length + '</strong> elements matching "' + query + '"';
            } else {
                text = 'No results matching "' + query + '"';
            }
        },
        onMouseEnter: function (node, a, item, event) {

            if (item.group === "country") {
                $(a).append('<span class="flag-chart flag-' + item.display.replace(' ', '-').toLowerCase() + '"></span>')
            }

        },
        onMouseLeave: function (node, a, item, event) {

            $(a).find('.flag-chart').remove();

        }
    }
});

if ($('.dt-hotel.flt-dest').length > 0) {
    $('.dt-hotel.flt-dest').typeahead({
    dynamic: true,
    blurOnTab: false,
    minLength: 1,
    maxItem: 20,
    template: "{{display}} ",
    emptyTemplate: function (query) {
        if (query.length > 0) {
            return 'No results found for "' + query + '"';
        }
    },
    source: {
        City: {
            ajax: function (query) {
                return {
                    type: "POST",
                    url: "/hotel/GetFltDestination_v2",
                    path: "data.City",
                    data: {
                        data: query
                    }
                }
            }
        },
    },
    callback: {
        onNavigateAfter: function (node, lis, a, item, query, event) {
            if (~[38, 40].indexOf(event.keyCode)) {
                var resultList = node.closest("form").find("ul.typeahead__list"),
                    activeLi = lis.filter("li.active"),
                    offsetTop = activeLi[0] && activeLi[0].offsetTop - (resultList.height() / 2) || 0;

                resultList.scrollTop(offsetTop);
            }

        },
        onClickAfter: function (node, a, item, event) {
            event.preventDefault();
        },
        onResult: function (node, query, result, resultCount) {
            if (query === "") return;

            var text = "";
            if (result.length > 0 && result.length < resultCount) {
                text = "Showing <strong>" + result.length + "</strong> of <strong>" + resultCount + '</strong> elements matching "' + query + '"';
            } else if (result.length > 0) {
                text = 'Showing <strong>' + result.length + '</strong> elements matching "' + query + '"';
            } else {
                text = 'No results matching "' + query + '"';
            }
        },
    }
});
}

$('.ht-filter-property').typeahead({
    dynamic: true,
    compression: true,
    minLength: 0,
    searchOnFocus: true,
    maxItem: 20,
    order: "asc",
    group: {
        key: "City",
    },
    template: '<span>' +
        '<span class="name">{{name}}</span>' +
        '<span class="division val">{{Name}}</span>' +
        //'<div><small><i>{{City}}</i></small></div>' + 
        '</span>',
    correlativeTemplate: true,
    emptyTemplate: function (query) {
        if (query.length > 0) {
            return 'No results found for "' + query + '"';
        }
    },
    display: ["Name", "City"],
    source: {
        Property: {
            compression: true,
            ajax: function (query) {
                return {
                    type: "GET",
                    url: "/hotel/getpropertyname_v2" + location.search,
                    data: {
                        data: query
                    }
                }
            }
        },
    },
    callback: {
        onNavigateAfter: function (node, lis, a, item, query, event) {
            if (~[38, 40].indexOf(event.keyCode)) {
                var resultList = node.closest("form").find("ul.typeahead__list"),
                    activeLi = lis.filter("li.active"),
                    offsetTop = activeLi[0] && activeLi[0].offsetTop - (resultList.height() / 2) || 0;

                resultList.scrollTop(offsetTop);
            }

        },
        onClickAfter: function (node, a, item, event) {
            event.preventDefault();
        },
        onResult: function (node, query, result, resultCount) {
            if (query === "") return;

            var text = "";
            if (result.length > 0 && result.length < resultCount) {
                text = "Showing <strong>" + result.length + "</strong> of <strong>" + resultCount + '</strong> elements matching "' + query + '"';
            } else if (result.length > 0) {
                text = 'Showing <strong>' + result.length + '</strong> elements matching "' + query + '"';
            } else {
                text = 'No results matching "' + query + '"';
            }
        },
        onMouseEnter: function (node, a, item, event) {

            if (item.group === "country") {
                $(a).append('<span class="flag-chart flag-' + item.display.replace(' ', '-').toLowerCase() + '"></span>')
            }

        },
    }
});