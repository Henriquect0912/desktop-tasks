using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.DisplayLayout;
using HojeWidget.Services.Startup;

namespace HojeWidget.Tests;

public class PersistenceTests
{
    [Fact]
    public void BancoECriadoComOEsquemaEsperado()
    {
        using var temp = new TempDatabase();

        var tables = temp.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
            var names = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        });

        Assert.Contains("tasks", tables);
        Assert.Contains("settings", tables);
        Assert.Contains("display_layouts", tables);
        Assert.Contains("daily_review", tables);
        Assert.Contains("schema_info", tables);
    }

    [Fact]
    public void ReabrirOBancoNaoApagaOsDados()
    {
        using var temp = new TempDatabase();
        var settings = new SettingsRepository(temp.Database);
        settings.SetInt(SettingsRepository.ThemeKey, (int)AppTheme.Translucent);

        temp.Reopen();

        var reopened = new SettingsRepository(temp.Database);
        Assert.Equal((int)AppTheme.Translucent, reopened.GetInt(SettingsRepository.ThemeKey, 0));
    }

    [Fact]
    public void MigracaoEIdempotente()
    {
        using var temp = new TempDatabase();
        temp.Reopen();
        temp.Reopen();

        var version = temp.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM schema_info;";
            return Convert.ToInt32(command.ExecuteScalar());
        });

        Assert.Equal(1, version);
    }

    [Fact]
    public void TemaEOpcaoDeInicializacaoPersistem()
    {
        using var temp = new TempDatabase();
        var settings = new SettingsRepository(temp.Database);

        settings.SetInt(SettingsRepository.ThemeKey, (int)AppTheme.Translucent);
        settings.SetBool(SettingsRepository.StartupInitializedKey, true);

        temp.Reopen();

        var reopened = new SettingsRepository(temp.Database);
        Assert.Equal((int)AppTheme.Translucent, reopened.GetInt(SettingsRepository.ThemeKey, 0));
        Assert.True(reopened.GetBool(SettingsRepository.StartupInitializedKey, false));
    }

    [Fact]
    public void ValorDeConfiguracaoCorrompidoUsaOPadrao()
    {
        using var temp = new TempDatabase();
        var settings = new SettingsRepository(temp.Database);

        settings.SetRaw(SettingsRepository.ThemeKey, "isto-nao-e-um-numero");

        Assert.Equal((int)AppTheme.Black, settings.GetInt(SettingsRepository.ThemeKey, (int)AppTheme.Black));
    }

    [Fact]
    public void LayoutEGuardadoPorAssinatura()
    {
        using var temp = new TempDatabase();
        var repository = new DisplayLayoutRepository(temp.Database);

        var dual = DisplaySignature.Compute(Monitors.Dual());
        var single = DisplaySignature.Compute(Monitors.Single());
        var now = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        repository.Save(new StoredLayout(dual, @"\\.\DISPLAY2", 100, 20, 410, 300, now));
        repository.Save(new StoredLayout(single, @"\\.\DISPLAY1", 40, 400, 380, 260, now));

        temp.Reopen();
        var reopened = new DisplayLayoutRepository(temp.Database);

        var dualLayout = reopened.Get(dual);
        var singleLayout = reopened.Get(single);

        Assert.NotNull(dualLayout);
        Assert.NotNull(singleLayout);
        Assert.Equal(@"\\.\DISPLAY2", dualLayout!.MonitorDevice);
        Assert.Equal(100, dualLayout.XDip);
        Assert.Equal(@"\\.\DISPLAY1", singleLayout!.MonitorDevice);
        Assert.Equal(400, singleLayout.YDip);
    }

    [Fact]
    public void SalvarUmaAssinaturaNaoSobrescreveAOutra()
    {
        using var temp = new TempDatabase();
        var repository = new DisplayLayoutRepository(temp.Database);
        var now = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        var dual = DisplaySignature.Compute(Monitors.Dual());
        var single = DisplaySignature.Compute(Monitors.Single());

        repository.Save(new StoredLayout(dual, @"\\.\DISPLAY2", 100, 20, 410, 300, now));
        repository.Save(new StoredLayout(single, @"\\.\DISPLAY1", 40, 400, 380, 260, now));
        repository.Save(new StoredLayout(dual, @"\\.\DISPLAY2", 700, 30, 410, 300, now));

        Assert.Equal(700, repository.Get(dual)!.XDip);
        Assert.Equal(40, repository.Get(single)!.XDip);
    }

    [Fact]
    public void LayoutInexistenteRetornaNulo()
    {
        using var temp = new TempDatabase();
        var repository = new DisplayLayoutRepository(temp.Database);

        Assert.Null(repository.Get("assinatura-que-nunca-existiu"));
    }

    [Fact]
    public void InicializacaoEAbstraidaENaoTocaNoRegistroReal()
    {
        IStartupRegistration registration = new InMemoryStartupRegistration();

        Assert.False(registration.IsEnabled);

        registration.Enable();
        Assert.True(registration.IsEnabled);

        registration.Disable();
        Assert.False(registration.IsEnabled);
    }
}
