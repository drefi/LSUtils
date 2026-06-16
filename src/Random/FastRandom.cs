namespace LSUtils.FastRandom;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

public static class FastRandom {
    // Algoritmo XORShift de 32 bits: Ultra-rápido e determinístico por thread
    private static uint _state = 123456789;

    private const int TableSize = 512; // Deve ser potência de 2 para usar Bitmask (Mascara de bit)
    private const int TableMask = TableSize - 1;
    private static readonly Vector2[] _directionTable;

    static FastRandom() {
        _directionTable = new Vector2[TableSize];
        for (int i = 0; i < TableSize; i++) {
            // Pré-calcula os vetores unitários ao redor de um círculo
            float angle = (float)(i * (Math.PI * 2.0) / TableSize);
            _directionTable[i] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }
    }

    /// <summary>
    /// Retorna um valor inteiro pseudo-aleatório usando apenas operações de deslocamento de bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Next() {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }
    public static float NextFloat() {
        // Converte o inteiro para um float entre 0 e 1
        // Qual a vantagem de ser entre 0 e 1? Facilita o cálculo de probabilidades e multiplicadores sem precisar de divisões adicionais.
        return Next() / (float)uint.MaxValue;
    }
    public static double NextDouble() {
        // Converte o inteiro para um double entre 0 e 1
        return Next() / (double)uint.MaxValue;
    }

    /// <summary>
    /// Retorna um vetor de direção aleatório pré-calculado em O(1) sem calcular Trigonometria.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 GetRandomDirection() {
        // O operador & (Bitwise AND) com uma máscara de potência de 2 
        // substitui o operador de módulo (%) que é muito mais lento na CPU.
        uint index = Next() & TableMask;
        return _directionTable[index];
    }
}
