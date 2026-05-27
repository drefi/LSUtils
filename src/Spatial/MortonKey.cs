namespace LSUtils.Spatial;

public static class MortonKey {
    // Expande um inteiro de 16 bits separando os bits com zeros intermediários
    // Ex: 0000 0000 ABCD EFGH -> 0A0B 0C0D 0E0F 0G0H
    public static uint Interleave16(uint x) {
        x = (x | (x << 8)) & 0x00FF00FF;
        x = (x | (x << 4)) & 0x0F0F0F0F;
        x = (x | (x << 2)) & 0x33333333;
        x = (x | (x << 1)) & 0x55555555;
        return x;
    }

    /// <summary>
    /// Gera uma chave única de 32 bits combinando X e Y.
    /// Suporta coordenadas de célula de 0 a 65535.
    /// </summary>
    public static uint Create2D(int cellX, int cellY) {
        if (cellX < 0 || cellY < 0) return 0; // Proteção simples para limites
        return (Interleave16((uint)cellY) << 1) | Interleave16((uint)cellX);
    }
}
