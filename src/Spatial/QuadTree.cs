namespace LSUtils.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;
/// <summary>
/// Implementação de QuadTree para particionamento espacial hierárquico 2D.
/// Divide recursivamente o espaço em quadrantes para consultas espaciais eficientes.
/// </summary>
/// <typeparam name="T">Tipo dos objetos armazenados.</typeparam>
public class QuadTree<T> : ISpatialIndex<T> where T : notnull {
    private readonly Bounds _bounds;
    private readonly int _capacity;
    private readonly List<QuadTreeEntry> _entries;
    private QuadTree<T>[]? _children;
    private bool _isDivided;
    private int _count;

    /// <summary>
    /// Número total de objetos na árvore (incluindo subnós).
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Limites espaciais desta árvore.
    /// </summary>
    public Bounds Bounds => _bounds;

    /// <summary>
    /// Capacidade máxima de objetos antes de subdividir.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Cria uma nova QuadTree.
    /// </summary>
    /// <param name="bounds">Limites espaciais da árvore.</param>
    /// <param name="capacity">Número máximo de objetos por nó antes de subdividir (padrão: 4).</param>
    public QuadTree(Bounds bounds, int capacity = 4) {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _bounds = bounds;
        _capacity = capacity;
        _entries = new List<QuadTreeEntry>(capacity);
        _isDivided = false;
        _count = 0;
    }

    /// <summary>
    /// Insere um objeto na árvore.
    /// </summary>
    public bool Insert(T item, Bounds bounds) {
        // Verifica se o objeto está dentro dos limites da árvore
        if (!_bounds.Intersects(bounds))
            return false;

        // Se não está subdividida e há espaço, adiciona aqui
        if (!_isDivided && _entries.Count < _capacity) {
            _entries.Add(new QuadTreeEntry(item, bounds));
            _count++;
            return true;
        }

        // Subdivide se necessário
        if (!_isDivided) {
            Subdivide();
        }

        // Tenta inserir nos filhos
        bool inserted = false;
        foreach (var child in _children!) {
            if (child.Insert(item, bounds)) {
                inserted = true;
            }
        }

        if (inserted) {
            _count++;
        }

        return inserted;
    }

    /// <summary>
    /// Consulta objetos dentro de uma área.
    /// </summary>
    public IReadOnlyList<T> Query(Bounds area) {
        var result = new List<T>();
        Query(area, result);
        return result;
    }

    private void Query(Bounds area, List<T> result) {
        // Se a área não intersecta, retorna vazio
        if (!_bounds.Intersects(area))
            return;

        // Adiciona objetos deste nó que intersectam a área
        foreach (var entry in _entries) {
            if (area.Intersects(entry.Bounds)) {
                result.Add(entry.Item);
            }
        }

        // Consulta recursivamente nos filhos
        if (_isDivided) {
            foreach (var child in _children!) {
                child.Query(area, result);
            }
        }
    }

    /// <summary>
    /// Remove um objeto da árvore.
    /// </summary>
    public bool Remove(T item) {
        // Tenta remover deste nó
        for (int i = _entries.Count - 1; i >= 0; i--) {
            if (EqualityComparer<T>.Default.Equals(_entries[i].Item, item)) {
                _entries.RemoveAt(i);
                _count--;
                return true;
            }
        }

        // Tenta remover dos filhos
        if (_isDivided) {
            foreach (var child in _children!) {
                if (child.Remove(item)) {
                    _count--;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Remove todos os objetos da árvore.
    /// </summary>
    public void Clear() {
        _entries.Clear();
        _children = null;
        _isDivided = false;
        _count = 0;
    }

    /// <summary>
    /// Subdivide este nó em 4 quadrantes.
    /// </summary>
    private void Subdivide() {
        float x = _bounds.X;
        float y = _bounds.Y;
        float w = _bounds.Width / 2;
        float h = _bounds.Height / 2;

        var nw = new Bounds(x - w / 2, y - h / 2, w, h); // Northwest
        var ne = new Bounds(x + w / 2, y - h / 2, w, h); // Northeast
        var sw = new Bounds(x - w / 2, y + h / 2, w, h); // Southwest
        var se = new Bounds(x + w / 2, y + h / 2, w, h); // Southeast

        _children = new[]
        {
            new QuadTree<T>(nw, _capacity),
            new QuadTree<T>(ne, _capacity),
            new QuadTree<T>(sw, _capacity),
            new QuadTree<T>(se, _capacity)
        };

        _isDivided = true;

        // Redistribui objetos existentes para os filhos
        var entriesToRedistribute = _entries.ToList();
        _entries.Clear();

        foreach (var entry in entriesToRedistribute) {
            bool inserted = false;
            foreach (var child in _children) {
                if (child.Insert(entry.Item, entry.Bounds)) {
                    inserted = true;
                }
            }

            // Se não coube em nenhum filho, mantém no pai
            if (!inserted) {
                _entries.Add(entry);
            }
        }
    }

    /// <summary>
    /// Retorna uma representação visual da árvore (para debug).
    /// </summary>
    public void DebugPrint(int depth = 0) {
        string indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}QuadTree at ({_bounds.X}, {_bounds.Y}) [{_bounds.Width}x{_bounds.Height}] - {_entries.Count} items");

        if (_isDivided && _children != null) {
            foreach (var child in _children) {
                child.DebugPrint(depth + 1);
            }
        }
    }

    private readonly record struct QuadTreeEntry(T Item, Bounds Bounds);
}
