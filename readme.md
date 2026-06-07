
dotnet tool install dotnet-ef

dotnet ef migrations --project App.DAL.EF --startup-project WebApp add Initial
dotnet ef database update --project App.DAL.EF --startup-project WebApp

dotnet aspnet-codegenerator controller -name AssetsController -m  Asset -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f

dotnet aspnet-codegenerator controller -name AssetsController  -m  App.Domain.Asset        -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
