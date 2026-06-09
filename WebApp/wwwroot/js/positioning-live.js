// Live positioning client.
//
// Connects to the /hubs/positioning SignalR hub (WebSocket transport)
// and dispatches incoming messages as DOM CustomEvents so views can
// listen without depending on SignalR directly:
//
//   window.addEventListener("positioning:position", e => {
//       const msg = e.detail;   // PositionResultMessage
//   });
//   window.addEventListener("positioning:raw", e => {
//       const msg = e.detail;   // RawMeasurementMessage
//   });
//
// Requires @microsoft/signalr (loaded from the layout / view).

(function () {
    "use strict";

    if (typeof signalR === "undefined") {
        console.error("[positioning] signalR client library not loaded.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/positioning")
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.on("PositionResult", function (msg) {
        window.dispatchEvent(new CustomEvent("positioning:position", { detail: msg }));
    });

    connection.on("RawMeasurement", function (msg) {
        window.dispatchEvent(new CustomEvent("positioning:raw", { detail: msg }));
    });

    async function start() {
        try {
            await connection.start();
            console.info("[positioning] connected to /hubs/positioning");
            window.dispatchEvent(new CustomEvent("positioning:connected"));
        } catch (err) {
            console.warn("[positioning] connection failed, retrying in 5s", err);
            setTimeout(start, 5000);
        }
    }

    connection.onreconnected(() => window.dispatchEvent(new CustomEvent("positioning:connected")));
    connection.onclose(()       => window.dispatchEvent(new CustomEvent("positioning:disconnected")));

    // Expose so views can call JoinSession / LeaveSession.
    window.PositioningHub = {
        connection,
        joinSession:  (sessionId) => connection.invoke("JoinSession",  sessionId),
        leaveSession: (sessionId) => connection.invoke("LeaveSession", sessionId),
    };

    start();
})();
