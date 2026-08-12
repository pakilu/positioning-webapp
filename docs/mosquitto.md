# Running the local Mosquitto broker

The MQTT ingestion path (`WebApp/Services/MqttIngestService.cs`) expects a
broker at `localhost:1883` by default. On Windows, start the bundled
Mosquitto broker from an **elevated** command prompt (needed so the service
can bind the default port and use the configured log destinations):

```cmd
"C:\Program Files\Mosquitto\mosquitto.exe" -c "C:\Program Files\Mosquitto\mosquitto.conf" -v
```

The `-v` flag turns on verbose logging so you can see chip registration and
raw-measurement traffic scroll past. See `readme.md` → **Configuration →
`Mqtt`** for the topics the webapp subscribes/publishes to.
