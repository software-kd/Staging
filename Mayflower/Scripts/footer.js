
$(function () {
    $("#subcribe-btn").click(function () {
        var sEmail = $('#email').val();
        if ($.trim(sEmail).length == 0) {
            alert('Please enter valid email address');
            //returnToPreviousPage();
            $('#subcribeForm').submit(function (e) {
                e.preventDefault();
            });

            //e.preventDefault();
        } else if (!validateEmail(sEmail)) {
            alert('Invalid Email Address');

            $('#subcribeForm').submit(function (e) {
                e.preventDefault();
            });

            //e.preventDefault();
        } else {
            //alert('Thanks for subcription');
            $('#subcribeForm').unbind().submit();

        }
    })
});

/* jQuery Validate Emails with Regex */
function validateEmail(Email) {
    var pattern = /^(([^<>()\[\]\\.,;:\s@"]+(\.[^<>()\[\]\\.,;:\s@"]+)*)|(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$/;

    return $.trim(Email).match(pattern) ? true : false;
}