namespace LSUtils;
/// <summary>
/// A delegate with two parameters.
/// </summary>
/// <typeparam name="T1">The type of the first parameter.</typeparam>
/// <typeparam name="T2">The type of the second parameter.</typeparam>
/// <param name="arg1">The first parameter.</param>
/// <param name="arg2">The second parameter.</param>
public delegate void LSAction<in T1, in T2>(T1 arg1, T2 arg2);
