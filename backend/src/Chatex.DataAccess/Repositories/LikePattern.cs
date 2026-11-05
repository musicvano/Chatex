namespace Chatex.DataAccess.Repositories;

internal static class LikePattern
{
    /// <summary>Pass to <c>EF.Functions.ILike</c>; without it Npgsql emits <c>ESCAPE ''</c> and the escaping is ignored.</summary>
    public const string Escape = @"\";

    /// <summary>Builds a LIKE pattern matching <paramref name="text"/> anywhere, with wildcards in it escaped.</summary>
    public static string Contains(string text) =>
        "%" + text.Replace(Escape, Escape + Escape).Replace("%", Escape + "%").Replace("_", Escape + "_") + "%";
}
