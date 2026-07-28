namespace Parcelly.Models;

public enum PaymentMode
{
    /// <summary>Só marca como paga manualmente.</summary>
    Manual = 0,

    /// <summary>Considera paga automaticamente no mês da parcela.</summary>
    Automatic = 1,

    /// <summary>Pode marcar manual; se o mês da parcela chegar, também considera paga.</summary>
    Mixed = 2
}
