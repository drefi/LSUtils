namespace LSUtils;
/// <summary>
/// A delegate with one parameter.
/// </summary>
/// <typeparam name="T">The type of the parameter.</typeparam>
/// <param name="obj">The parameter.</param>
public delegate void LSAction<in T>(T obj);
