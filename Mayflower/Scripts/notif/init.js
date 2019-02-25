$(function () {
    // Declare a proxy to reference the hub.
    var notif = $.connection.notificationHub;
    // Create a function that the hub can call to broadcast messages.
    notif.client.pushSingle = function (res) {
        // Html encode display name and message.
        var enRes = $('<div />').text(res).html();
        // Add the message to the page.
        $('body').append('<li>&nbsp;&nbsp;' + enRes + '</li>');
    };
    
    $.connection.hub.start().done(function () {
        notif.server.notif();
        /*$('#msg').click(function () {
            notif.server.send('');
        });*/
    });
});