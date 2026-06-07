
dotnet tool install dotnet-ef

dotnet ef migrations --project App.DAL.EF --startup-project WebApp add Initial
dotnet ef database update --project App.DAL.EF --startup-project WebApp

dotnet aspnet-codegenerator controller -name ChipsController -m Chip -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name PositionResultsController -m  PositionResult -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name RawMeasurementsController -m  RawMeasurement -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionsController -m  Session -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionConfigsController -m  SessionConfig -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionConfigChipsController -m  SessionConfigChip -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f


dotnet aspnet-codegenerator controller -name ChipsController -m App.Domain.Chip -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name PositionResultsController -m App.Domain.PositionResult -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name RawMeasurementsController -m App.Domain.RawMeasurement -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name SessionsController -m App.Domain.Session -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name SessionConfigsController -m App.Domain.SessionConfig -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name SessionConfigChipsController -m App.Domain.SessionConfigChip -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f

