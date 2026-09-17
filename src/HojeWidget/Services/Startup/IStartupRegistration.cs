namespace HojeWidget.Services.Startup;

/// <summary>
/// Inicializacao com o Windows. Abstraida para que os testes nunca toquem
/// no registro real da maquina.
/// </summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void Enable();

    void Disable();
}

/// <summary>Implementacao em memoria usada por testes.</summary>
public sealed class InMemoryStartupRegistration : IStartupRegistration
{
    public bool IsEnabled { get; private set; }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;
}
