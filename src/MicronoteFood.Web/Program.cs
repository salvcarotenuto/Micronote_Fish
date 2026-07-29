using MicronoteFood.Web.Data;
using MicronoteFood.Web.Services;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Secrets.json",
        optional: true,
        reloadOnChange: false);
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<ArticleListPdfService>();
builder.Services.AddSingleton<CustomerListPdfService>();
builder.Services.AddSingleton<SupplierListPdfService>();
builder.Services.AddSingleton<TrialBalancePdfService>();
builder.Services.AddSingleton<StoreMovementSummaryPdfService>();
builder.Services.AddScoped<TrialBalanceRepository>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ApplicationState>();
builder.Services.AddSingleton<MicronoteDatabaseOptions>();
builder.Services.AddScoped<CurrentCompanyContext>();
builder.Services.AddScoped<MicronoteServicePaths>();
builder.Services.AddScoped<MicronoteDb>();
builder.Services.AddScoped<ProgressiveCodeService>();
builder.Services.AddScoped<AccountingCauseRepository>();
builder.Services.AddScoped<AccountingMovementRepository>();
builder.Services.AddScoped<AccountingMovementSummaryRepository>();
builder.Services.AddScoped<AccountingStatementRepository>();
builder.Services.AddScoped<ActivityRepository>();
builder.Services.AddScoped<ArticleAccountRepository>();
builder.Services.AddScoped<ArticleRepository>();
builder.Services.AddScoped<BankRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<CustomerSupplierCategoryRepository>();
builder.Services.AddScoped<CashStatementRepository>();
builder.Services.AddScoped<DailySalesRepository>();
builder.Services.AddScoped<ChartAccountRepository>();
builder.Services.AddScoped<ComuniRepository>();
builder.Services.AddScoped<CustomerRepository>();
builder.Services.AddScoped<CounterSaleRepository>();
builder.Services.AddScoped<CustomerSupplierBalanceSummaryRepository>();
builder.Services.AddScoped<FishClassificationRepository>();
builder.Services.AddScoped<GroupRepository>();
builder.Services.AddScoped<InitialArticleStockRepository>();
builder.Services.AddScoped<InitialCustomerSupplierBalanceRepository>();
builder.Services.AddScoped<LedgerMasterRepository>();
builder.Services.AddScoped<LookupRepository>();
builder.Services.AddScoped<NationRepository>();
builder.Services.AddScoped<OpeningBalanceRepository>();
builder.Services.AddScoped<PaymentCodeRepository>();
builder.Services.AddScoped<PurchaseInvoiceRepository>();
builder.Services.AddScoped<SalesEntryRepository>();
builder.Services.AddScoped<SalesHistoryRepository>();
builder.Services.AddScoped<SalesDocumentRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<StockLoadRepository>();
builder.Services.AddScoped<StockUnloadRepository>();
builder.Services.AddScoped<StockMovementRepository>();
builder.Services.AddScoped<StockPurchaseStatsRepository>();
builder.Services.AddScoped<GroupPurchaseStatsRepository>();
builder.Services.AddScoped<StoreMovementSummaryRepository>();
builder.Services.AddScoped<SupplierRepository>();
builder.Services.AddScoped<SectorRepository>();
builder.Services.AddScoped<StoreRepository>();
builder.Services.AddScoped<SubgroupRepository>();
builder.Services.AddScoped<UnitMeasureRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<VatRateRepository>();
builder.Services.AddScoped<SmtpConnectionTester>();
builder.Services.AddScoped<MasterRepository>();
builder.Services.AddScoped<SystemAdminAuthService>();
builder.Services.AddScoped<ApplicationAuthService>();
builder.Services.AddScoped<ActivityLogService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<MicronoteServicePaths>().EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Configuration.GetValue("Hosting:EnableHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseSession();

app.Use(async (context, next) =>
{
    if (IsPublicRequest(context.Request))
    {
        await next();
        return;
    }

    var auth = context.RequestServices.GetRequiredService<ApplicationAuthService>();
    if (auth.IsCompanyLoggedIn())
    {
        await next();
        return;
    }

    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    context.Response.Redirect("/Login");
});app.UseAuthorization();

app.MapStaticAssets();
app.MapGet(
    "/api/comuni",
    async (
        string? q,
        ComuniRepository repository,
        CancellationToken cancellationToken) =>
        Results.Json(await repository.SearchAsync(q, cancellationToken)));
app.MapGet(
    "/api/lookup_anagrafiche",
    async (
        string? type,
        string? q,
        int? code,
        LookupRepository repository,
        CancellationToken cancellationToken) =>
    {
        if (code is not null)
        {
            return Results.Json(new
            {
                row = await repository.FindAnagraficaAsync(type, code.Value, cancellationToken)
            });
        }

        return Results.Json(new
        {
            rows = await repository.SearchAnagraficheAsync(type, q, cancellationToken)
        });
    });
app.MapGet(
    "/api/articoli/{code}",
    async (
        string code,
        ArticleRepository repository,
        CancellationToken cancellationToken) =>
    {
        var article = await repository.GetAsync(code, cancellationToken);
        return article is null
            ? Results.NotFound()
            : Results.Json(new
            {
                code = article.Code,
                description = article.Description,
                unitMeasure = article.SalesUnitCode ?? "",
                tare = article.Tare ?? 0,
                price = article.StandardCost ?? 0,
                lastPrice = article.LastCost ?? 0,
                stock = article.InitialWeight ?? 0,
                vatRate = article.VatRate ?? 0
            });
    });
app.MapGet(
    "/api/articoli",
    async (
        string? q,
        int? categoria,
        int? fornitore,
        ArticleRepository repository,
        CancellationToken cancellationToken) =>
    {
        var search = q?.Trim() ?? "";
        var rows = (await repository.ListAsync(cancellationToken))
            .Where(article => categoria is null or 0 || article.CategoryCode == categoria)
            .Where(article => fornitore is null or 0 || article.SupplierCode == fornitore)
            .Where(article =>
                search.Length == 0 ||
                article.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                article.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(article => new
            {
                code = article.Code,
                description = article.Description,
                unitMeasure = article.SalesUnitCode,
                purchaseUnitMeasure = article.SalesUnitCode,
                salesUnitMeasure = article.PurchaseUnitCode,
                tare = article.Tare,
                categoryCode = article.CategoryCode,
                category = article.CategoryDescription,
                group = article.GroupDescription,
                subgroup = article.SpeciesDescription,
                species = article.SpeciesDescription,
                origin = article.OriginDescription,
                supplierCode = article.SupplierCode,
                supplier = article.SupplierName,
                standardCost = article.StandardCost,
                standardPrice = article.StandardPrice,
                price = article.StandardCost,
                lastPrice = article.StandardCost,
                stock = article.InitialWeight,
                vatRate = article.VatRate
            })
            .ToArray();

        return Results.Json(new { rows });
    });
app.MapPost(
    "/api/settings/test-smtp",
    async (
        SmtpTestRequest? request,
        SmtpConnectionTester tester,
        CancellationToken cancellationToken) =>
        Results.Json(await tester.TestAsync(request, cancellationToken)));
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
static bool IsPublicRequest(HttpRequest request)
{
    var path = request.Path;
    if (!path.HasValue)
    {
        return false;
    }

    if (path.StartsWithSegments("/Login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Logout", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return Path.HasExtension(path.Value);
}








