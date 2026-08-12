> **Archived design log — 2026-08-12.** These notes were captured during early prototyping and later UX review. They are **not authoritative**; treat them as historical context, not a spec. Some items have since been addressed (e.g. the `Areas/Admin` vs. non-areas mismatch and the boilerplate Privacy page were resolved in change `cleanup-webapp-coherence`); others remain open and may become future proposals.

---

## Part 1 — Original next-steps.txt (early prototyping design log)

 1. The big picture                                                                                                                                                                 
                                                                                                                                                                                    
 ```                                                                                                                                                                                
   [ESP32+DW3000 anchors] ──┐                                                                                                                                                       
                            │  Wi-Fi                                                                                                                                                
   [ESP32+DW3000 tag(s)]  ──┤────► [Gateway/Coordinator]  ──HTTP/WebSocket──► [ASP.NET WebApp]                                                                                      
                            │       (one ESP32 OR MQTT broker                       │                                                                                               
                            │        OR direct from each node)                      ▼                                                                                               
                                                                                 Postgres                                                                                           
                                                                                    │                                                                                               
                                                                                    ▼                                                                                               
                                                                               Browser UI                                                                                           
                                                                               (SignalR live map)                                                                                   
 ```                                                                                                                                                                                
                                                                                                                                                                                    
 You have three realistic architecture choices. Pick one:                                                                                                                           
                                                                                                                                                                                    
 A. Each ESP32 talks directly to the webapp over HTTP/WebSocket — simplest, but every chip needs Wi‑Fi creds + your server URL, and the chips do their own ranging via DW3000 radio 
 between each other.                                                                                                                                                                
                                                                                                                                                                                    
 B. One ESP32 acts as a "gateway" — anchors do UWB ranging with the tag, the tag (or one designated anchor) collects all distance results via UWB and forwards over Wi‑Fi/HTTP to   
 the server. This matches how most DW3000 demo sketches (Makerfabs, Qorvo) already work.                                                                                            
                                                                                                                                                                                    
 C. MQTT broker in the middle — chips publish to topics, your webapp subscribes. Best if you'll have many nodes.                                                                    
                                                                                                                                                                                    
 For a small system I'd start with B.                                                                                                                                               
                                                                                                                                                                                    
 2. Device identity — how chips become entities                                                                                                                                     
                                                                                                                                                                                    
 Your Chip already has DeviceIdentifier. Use the DW3000/ESP32 MAC or a short UWB short address (e.g. "A1B2" or "24:6F:28:...") as the DeviceIdentifier. Flash each board with a     
 unique ID in NVS/EEPROM.                                                                                                                                                           
                                                                                                                                                                                    
 Workflow:                                                                                                                                                                          
 1. Boot ESP32, it has a hardcoded/NVS device_id.                                                                                                                                   
 2. On first contact, it POSTs POST /api/chips/register with { deviceIdentifier, firmwareVersion, role: "anchor"|"tag" }.                                                           
 3. The server either creates a new Chip row or returns the existing one's Id (Guid).                                                                                               
 4. The ESP32 caches that Guid (or just keeps using its short ID; the server resolves).                                                                                             
 5. In the UI you assign a Name, fixed anchor coordinates (you'll want to add XCoord/YCoord/ZCoord and IsAnchor to Chip), and put it into a SessionConfig.                          
                                                                                                                                                                                    
 You should add these fields to Chip.cs:                                                                                                                                            
                                                                                                                                                                                    
 ```csharp                                                                                                                                                                          
   public decimal? XCoord { get; set; }   // anchor position, null for tags                                                                                                         
   public decimal? YCoord { get; set; }                                                                                                                                             
   public decimal? ZCoord { get; set; }                                                                                                                                             
   public EChipRole Role { get; set; }    // Anchor | Tag                                                                                                                           
   public DateTime? LastSeenAt { get; set; }                                                                                                                                        
   public bool IsOnline { get; set; }                                                                                                                                               
 ```                                                                                                                                                                                
                                                                                                                                                                                    
 (Add an EChipRole enum under App.Domain/Enums, then create an EF migration.)                                                                                                       
                                                                                                                                                                                    
 3. Server endpoints you actually need                                                                                                                                              
                                                                                                                                                                                    
 Your scaffolded CRUD is fine for management, but for device ingestion add a dedicated controller — devices should not POST raw EF entities. Use DTOs:                              
                                                                                                                                                                                    
 ```csharp                                                                                                                                                                          
   // WebApp/ApiControllers/IngestController.cs                                                                                                                                     
   [ApiController, Route("api/ingest")]                                                                                                                                             
   public class IngestController : ControllerBase                                                                                                                                   
   {                                                                                                                                                                                
       private readonly AppDbContext _db;                                                                                                                                           
       private readonly IHubContext<PositioningHub> _hub;                                                                                                                           
       public IngestController(AppDbContext db, IHubContext<PositioningHub> hub)                                                                                                    
       { _db = db; _hub = hub; }                                                                                                                                                    
                                                                                                                                                                                    
       public record RegisterDto(string DeviceIdentifier, string Role, string? Firmware);                                                                                           
       public record MeasurementDto(string TagId, string AnchorId, double Distance,                                                                                                 
                                    double? Rssi, double? Snr, double? Quality,                                                                                                     
                                    long? DeviceTimestampMs);                                                                                                                       
       public record BatchDto(Guid SessionId, MeasurementDto[] Measurements);                                                                                                       
                                                                                                                                                                                    
       [HttpPost("register")]                                                                                                                                                       
       public async Task<IActionResult> Register(RegisterDto dto)                                                                                                                   
       {                                                                                                                                                                            
           var chip = await _db.Chips.FirstOrDefaultAsync(c => c.DeviceIdentifier == dto.DeviceIdentifier);                                                                         
           if (chip == null)                                                                                                                                                        
           {                                                                                                                                                                        
               chip = new Chip {                                                                                                                                                    
                   DeviceIdentifier = dto.DeviceIdentifier,                                                                                                                         
                   Name = dto.DeviceIdentifier,                                                                                                                                     
                   Role = Enum.Parse<EChipRole>(dto.Role, true)                                                                                                                     
               };                                                                                                                                                                   
               _db.Chips.Add(chip);                                                                                                                                                 
           }                                                                                                                                                                        
           chip.LastSeenAt = DateTime.UtcNow;                                                                                                                                       
           chip.IsOnline = true;                                                                                                                                                    
           await _db.SaveChangesAsync();                                                                                                                                            
           return Ok(new { chip.Id });                                                                                                                                              
       }                                                                                                                                                                            
                                                                                                                                                                                    
       [HttpPost("measurements")]                                                                                                                                                   
       public async Task<IActionResult> Ingest(BatchDto dto)                                                                                                                        
       {                                                                                                                                                                            
           // Resolve device-short-IDs → Chip.Id once                                                                                                                               
           var ids = dto.Measurements.SelectMany(m => new[] { m.TagId, m.AnchorId }).Distinct().ToArray();                                                                          
           var chips = await _db.Chips.Where(c => ids.Contains(c.DeviceIdentifier))                                                                                                 
                                      .ToDictionaryAsync(c => c.DeviceIdentifier, c => c);                                                                                          
                                                                                                                                                                                    
           var rows = dto.Measurements.Select(m => new RawMeasurement {                                                                                                             
               SessionId    = dto.SessionId,                                                                                                                                        
               TagChipId    = chips[m.TagId].Id,                                                                                                                                    
               AnchorChipId = chips[m.AnchorId].Id,                                                                                                                                 
               Distance     = (decimal)m.Distance,                                                                                                                                  
               Rssi  = m.Rssi  is double r ? (decimal?)r : null,                                                                                                                    
               Snr   = m.Snr   is double s ? (decimal?)s : null,                                                                                                                    
               Quality = m.Quality is double q ? (decimal?)q : null,                                                                                                                
               RecordedAt = DateTime.UtcNow                                                                                                                                         
           }).ToList();                                                                                                                                                             
                                                                                                                                                                                    
           _db.RawMeasurements.AddRange(rows);                                                                                                                                      
           await _db.SaveChangesAsync();                                                                                                                                            
                                                                                                                                                                                    
           // Trilaterate per tag and push to UI                                                                                                                                    
           foreach (var byTag in rows.GroupBy(r => r.TagChipId))                                                                                                                    
           {                                                                                                                                                                        
               var pos = Trilateration.Solve(byTag, _db);   // your math                                                                                                            
               if (pos is not null)                                                                                                                                                 
               {                                                                                                                                                                    
                   _db.PositionResults.Add(new PositionResult {                                                                                                                     
                       SessionId = dto.SessionId, TagChipId = byTag.Key,                                                                                                            
                       XCoord = pos.X, YCoord = pos.Y, ZCoord = pos.Z, Accuracy = pos.Err                                                                                           
                   });                                                                                                                                                              
                   await _hub.Clients.Group($"session-{dto.SessionId}")                                                                                                             
                       .SendAsync("position", new { tagId = byTag.Key, pos.X, pos.Y, pos.Z });                                                                                      
               }                                                                                                                                                                    
           }                                                                                                                                                                        
           await _db.SaveChangesAsync();                                                                                                                                            
           return Ok();                                                                                                                                                             
       }                                                                                                                                                                            
   }                                                                                                                                                                                
 ```                                                                                                                                                                                
                                                                                                                                                                                    
 4. Live updates to the browser — SignalR                                                                                                                                           
                                                                                                                                                                                    
 Add Microsoft.AspNetCore.SignalR (built-in). In Program.cs:                                                                                                                        
                                                                                                                                                                                    
 ```csharp                                                                                                                                                                          
   builder.Services.AddSignalR();                                                                                                                                                   
   // ...                                                                                                                                                                           
   app.MapHub<PositioningHub>("/hubs/positioning");                                                                                                                                 
 ```                                                                                                                                                                                
                                                                                                                                                                                    
 ```csharp                                                                                                                                                                          
   public class PositioningHub : Hub {                                                                                                                                              
       public Task JoinSession(Guid sessionId) =>                                                                                                                                   
           Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");                                                                                                    
   }                                                                                                                                                                                
 ```                                                                                                                                                                                
                                                                                                                                                                                    
 In a Razor view, the browser connects with @microsoft/signalr (CDN), draws anchors as fixed dots and tags moving in real time on a <canvas>/SVG floorplan.                         
                                                                                                                                                                                    
 5. ESP32 firmware side (C++)                                                                                                                                                       
                                                                                                                                                                                    
 Use WiFi.h + HTTPClient (or WebSocketsClient for lower latency). Skeleton for the gateway/tag that already has distance results from DW3000:                                       
                                                                                                                                                                                    
 ```cpp                                                                                                                                                                             
   #include <WiFi.h>                                                                                                                                                                
   #include <HTTPClient.h>                                                                                                                                                          
   #include <ArduinoJson.h>                                                                                                                                                         
                                                                                                                                                                                    
   const char* WIFI_SSID="...", *WIFI_PW="...";                                                                                                                                     
   const char* SERVER = "http://192.168.1.50:5000";                                                                                                                                 
   const char* DEVICE_ID = "TAG-001";          // unique per board                                                                                                                  
   const char* SESSION_ID = "....-guid-....";  // pushed via config endpoint                                                                                                        
                                                                                                                                                                                    
   void postMeasurements(const String& json) {                                                                                                                                      
     HTTPClient http;                                                                                                                                                               
     http.begin(String(SERVER) + "/api/ingest/measurements");                                                                                                                       
     http.addHeader("Content-Type", "application/json");                                                                                                                            
     int code = http.POST(json);                                                                                                                                                    
     http.end();                                                                                                                                                                    
   }                                                                                                                                                                                
                                                                                                                                                                                    
   void loop() {                                                                                                                                                                    
     // 1. run DW3000 two-way ranging against each known anchor                                                                                                                     
     //    -> dist_A1, dist_A2, dist_A3 ...                                                                                                                                         
     StaticJsonDocument<1024> doc;                                                                                                                                                  
     doc["sessionId"] = SESSION_ID;                                                                                                                                                 
     JsonArray arr = doc.createNestedArray("measurements");                                                                                                                         
     for (auto& r : ranges) {                                                                                                                                                       
       JsonObject m = arr.createNestedObject();                                                                                                                                     
       m["tagId"]    = DEVICE_ID;                                                                                                                                                   
       m["anchorId"] = r.anchorId;   // e.g. "ANC-01"                                                                                                                               
       m["distance"] = r.distanceM;                                                                                                                                                 
       m["rssi"]     = r.rssi;                                                                                                                                                      
     }                                                                                                                                                                              
     String out; serializeJson(doc, out);                                                                                                                                           
     postMeasurements(out);                                                                                                                                                         
     delay(100); // 10 Hz                                                                                                                                                           
   }                                                                                                                                                                                
 ```                                                                                                                                                                                
                                                                                                                                                                                    
 On boot, call /api/ingest/register once for the tag.                                                                                                                               
                                                                                                                                                                                    
 6. Trilateration                                                                                                                                                                   
                                                                                                                                                                                    
 Put the math in App.BLL (e.g. PositioningService). For 3 anchors at known (x,y) with distances d_i, do least‑squares: minimize Σ ((x−xi)² + (y−yi)² − di²)². Plenty of C# snippets 
 exist; start with the linearised closed‑form for an MVP, upgrade to NLLS later.                                                                                                    
                                                                                                                                                                                    
 7. CORS / security                                                                                                                                                                 
                                                                                                                                                                                    
 Add CORS so devices on the LAN can POST:                                                                                                                                           
                                                                                                                                                                                    
 ```csharp                                                                                                                                                                          
   builder.Services.AddCors(o => o.AddDefaultPolicy(p =>                                                                                                                            
       p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));                                                                                                                      
   app.UseCors();                                                                                                                                                                   
 ```                                                                                                                                                                                
                                                                                                                                                                                    
 For real deployment: require an X-Device-Token header per chip (store hash in Chip), and turn off AllowAnyOrigin.                                                                  
                                                                                                                                                                                    
 8. Concrete order of work for you                                                                                                                                                  
                                                                                                                                                                                    
 1. Add Role, XCoord/Y/Z, LastSeenAt, IsOnline to Chip; new migration.                                                                                                              
 2. Add IngestController with register + measurements DTOs above.                                                                                                                   
 3. Add SignalR + PositioningHub.                                                                                                                                                   
 4. Build a "Chips" management page (mostly done) where you set anchor coordinates + assign chips to a SessionConfig.                                                               
 5. Build a "Live Session" view: floorplan canvas that subscribes to the hub.                                                                                                       
 6. Flash 3 ESP32 anchors with fixed short IDs ANC-01/02/03, one ESP32 tag TAG-01. Get DW3000 two‑way ranging working between them first (Makerfabs/Qorvo examples), then add the   
    HTTP POST.                                                                                                                                                                      
 7. Verify rows appear in RawMeasurements, PositionResults updates, and the dot moves in the browser.          

---

## Part 2 — UX audit (originally in "cmd open as admin.txt")

 UX gaps                                                                                                                                                                                                                            
                                                                                                                                                                                                                                    
 ### 8. The Sessions creation step is the activation step                                                                                                                                                                           
                                                                                                                                                                                                                                    
 SessionsController.Create calls StartSession, so "Start session" immediately runs the pipeline. There is no draft state ever shown to the user, which is fine — but then ESessionStatus.Created is dead, and the help in the       
 Sessions/Edit view ("Change session status from the Live page so timestamps stay consistent") is mostly aspirational. Either:                                                                                                      
 - Don't activate on Create — let "Start session" be a separate explicit step on the Live page; or                                                                                                                                  
 - Remove the Created status and rename the button to make the immediate-activation behavior obvious.                                                                                                                               
                                                                                                                                                                                                                                    
 ### 9. "Layout chips" should not be a top-level resource                                                                                                                                                                           
                                                                                                                                                                                                                                    
 SessionConfigChips/Index is a flat list across all layouts. To set up a 5-anchor room you click Create five times and each time pick the room from a dropdown. UX-wise this is the most painful flow in the app. Suggestions,      
 easiest to hardest:                                                                                                                                                                                                                
 - Drop "Layout chips" from the top nav; expose it from SessionConfigs/Details as an "Anchors and tags" sub-section.                                                                                                                
 - Add a filter ?sessionConfigId=… to Index, and have SessionConfigs/Details link to it pre-filtered.                                                                                                                               
 - Best: turn SessionConfigs/Details into the editor — show a table of anchors (with inline X/Y/Z editors) and tags, with "Add anchor"/"Add tag" buttons that pre-fill the layout. Also include a tiny preview map of where the     
   anchors sit.                                                                                                                                                                                                                     
                                                                                                                                                                                                                                    
 ### 10. SessionConfigs/Details shows no children                                                                                                                                                                                   
                                                                                                                                                                                                                                    
 It lists Name/Description/PlannedDuration/timestamps and nothing else. The user has no idea what's in the layout. At minimum, embed the list of chips and roles with a "Set up chips" CTA.                                         
                                                                                                                                                                                                                                    
 ### 11. Sessions/Index lacks the most important info                                                                                                                                                                               
                                                                                                                                                                                                                                    
 You see Name/Status/timestamps but not:                                                                                                                                                                                            
 - how many anchors/tags the layout has,                                                                                                                                                                                            
 - whether a Live URL is reachable,                                                                                                                                                                                                 
 - whether the session is currently producing fixes (e.g. "last fix 3 s ago").                                                                                                                                                      
                                                                                                                                                                                                                                    
 Also Status should be color-badged (Active=green etc.) and timestamps should be local + formatted.                                                                                                                                 
                                                                                                                                                                                                                                    
 ### 12. Empty/intro states are missing                                                                                                                                                                                             
                                                                                                                                                                                                                                    
 First-run users see an empty table on every Index. Add empty states: "No chips yet — chips auto-register when an ESP32 publishes to the broker, or click Register chip to add one by hand." Same for layouts/sessions.             
                                                                                                                                                                                                                                    
 ### 13. SessionConfigChips/Create and Edit are not validation-symmetric with the controller                                                                                                                                        
                                                                                                                                                                                                                                    
 The controller flips XCoord/YCoord/ZCoord to null for Tags and requires X+Y for Anchors. The view hides those fields when Tag is selected but a stale Z from a previous Anchor selection still gets posted because:                
                                                                                                                                                                                                                                    
 ```js                                                                                                                                                                                                                              
   input.disabled = !isAnchor; if (!isAnchor) input.value = "";                                                                                                                                                                     
 ```                                                                                                                                                                                                                                
                                                                                                                                                                                                                                    
 A disabled input doesn't post, but if the role is Anchor, X and Y are required while Z is currently optional in the validator yet shown the same way as X/Y. Either label Z as "(optional)" or also require it. Same view used for 
 Edit, so editing a previously valid Anchor and switching to Tag does the right thing — good — but the "Use as" labels on the Role select come from enum text and the JS string-matches "anchor" case-insensitively, which is       
 fragile if you ever localize. Compare roleSelect.value to the enum int instead.                                                                                                                                                    
                                                                                                                                                                                                                                    
 ### 14. The "Deactivate session" button copy is misleading                                                                                                                                                                         
                                                                                                                                                                                                                                    
 On Sessions/Live, when Active, the button text says "Deactivate session" but it POSTs to Finish (terminal). Call it "Finish session" (or add a separate "Pause/Cancel"). The variable is even called activateButtonText which is   
 reused for the opposite action.                                                                                                                                                                                                    
                                                                                                                                                                                                                                    
 ### 15. Live map shows only one tag                                                                                                                                                                                                
                                                                                                                                                                                                                                    
 Sessions/Live keeps a single #tag-marker and reuses it for every incoming fix. With two tags in a session the marker bounces between them. Render one marker per known tag (you already serialize TAGS).                           
                                                                                                                                                                                                                                    
 ### 16. No stale-fix indicator                                                                                                                                                                                                     
                                                                                                                                                                                                                                    
 The map keeps the last marker position forever after measurements stop. Fade or hide markers after, e.g., 5 s of silence; same for the #latest-fix text. Also color conn-status differently for "reconnecting".                    
                                                                                                                                                                                                                                    
 ### 17. Chip / device-identifier UX inconsistency                                                                                                                                                                                  
                                                                                                                                                                                                                                    
 The chip Create form has a MAC-formatting blur handler (normalizes aabb... → AA:BB:...), but the MQTT auto-registration writes the raw DeviceIdentifier value used by the firmware (often a short ID like 0x01). A user who        
 manually registers a chip with the prettified MAC will not match incoming tagDeviceId="0x01". Either:                                                                                                                              
 - Document the expected format on the Create form ("must match the value your firmware publishes on the chip-registration topic — usually the short ID, not the MAC"), and disable the prettifier when the input doesn't look like 
   a MAC; or                                                                                                                                                                                                                        
 - Move the auto-formatter onto a separate "MAC address" field and treat DeviceIdentifier as opaque.                                                                                                                                
                                                                                                                                                                                                                                    
 ### 18. No "online/last seen" signal for chips                                                                                                                                                                                     
                                                                                                                                                                                                                                    
 next-steps.txt itself flagged adding LastSeenAt/IsOnline to Chip. Without it, you can't tell from the UI whether an anchor is actually publishing — which is the #1 question a user has when no fixes appear. Update LastSeenAt    
 from MqttIngestService.HandleRawAsync and HandleRegistrationAsync, and show a green/grey dot in Chips and Layout chips lists.                                                                                                      
                                                                                                                                                                                                                                    
 ### 19. Navigation top bar exposes admin-only resources                                                                                                                                                                            
                                                                                                                                                                                                                                    
 "Position results", "Raw measurements", "Layout chips" are largely debugging surfaces and clutter the bar for normal users. Group them under an "Admin" dropdown (the controllers already live under Areas/Admin/Controllers       
 namespace-wise, although they don't actually use ASP.NET Areas routing — the namespace is misleading).                                                                                                                             
                                                                                                                                                                                                                                    
 ### 20. Areas vs. non-areas mismatch                                                                                                                                                                                               
                                                                                                                                                                                                                                    
 Areas/Admin/Controllers/* controllers have no [Area("Admin")] attribute and Program.cs has no MapAreaControllerRoute. So everything is served from the default route, but the namespace and folder structure suggest otherwise.    
 Pick one: either fully adopt Areas (with /Admin/Chips/... URLs, area-attributed controllers, and a separate layout) or move the files back into Controllers/ and Views/. Right now it confuses anyone reading the code.            
                                                                                                                                                                                                                                    
 ### 21. Timestamps everywhere are raw UTC                                                                                                                                                                                          
                                                                                                                                                                                                                                    
 Sessions/Index, Details, SessionConfigs/Details all render via @Html.DisplayFor, so you see 6/16/2025 12:00:00 AM UTC. Apply a [DisplayFormat] on the domain types or format in the views with .ToLocalTime().ToString(...) like   
 Sessions/Live already does.                                                                                                                                                                                                        
                                                                                                                                                                                                                                    
 ### 22. Default scaffold titles                                                                                                                                                                                                    
                                                                                                                                                                                                                                    
 Most pages still show <h1>Index</h1>, <h1>Create</h1>, etc., or even <h4>SessionConfig</h4>. Rename to human strings ("Room layouts", "New room layout", "Edit room layout").                                                      
                                                                                                                                                                                                                                    
 ### 23. Privacy page is still the template default                                                                                                                                                                                 
                                                                                                                                                                                                                                    
 Probably not worth a delete but doesn't belong if you're publishing this anywhere.                                                                                                                                                 
                                                                                                                                                                                                                                    
 ────────────────────────────────────────────────────────────────────────────────                                                                                                                                                   
                                                                                                                                                                                                                                    
 Smaller polish                                                                                                                                                                                                                     
                                                                                                                                                                                                                                    
 - Sessions/Live reads Model.Status == ESessionStatus.Active twice for branching but only renders the form once each branch — fine, but consider showing both Finish and Cancel buttons while Active.                               
 - positioning-live.js always logs at Information level — switch to Warning for production builds, or expose via a global flag.                                                                                                     
 - Home/Index lists "1. Register physical chips" but with MQTT auto-registration enabled this is optional — clarify "(optional — chips also auto-register from the broker)".                                                        
 - Sessions/Create has a "Name" field after "Session configuration"; pre-fill it with the layout name + timestamp if empty so the user can just click through.                                                                      
 - The 2D map ignores z; either label it "X–Y projection" or add a small Z indicator next to each marker.                                                                                                                           
 - MqttIngestService.StartAsync subscribes before StartAsync connects — works with MQTTnet's managed client but might race on first reconnect; consider swapping the order for clarity.                                             
 - Areas/Admin/Controllers/SessionConfigChipsController doesn't enforce uniqueness of (SessionConfigId, ChipId) — adding the same chip twice to a layout silently produces two rows; the pipeline picks one arbitrarily. Add a      
   unique index in EF and a friendly model error.              
