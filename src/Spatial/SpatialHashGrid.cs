namespace LSUtils.Spatial;

using System;
using System.Collections.Generic;
/// <summary>
/// Implementação de grade hash espacial para consultas 2D com atualização frequente.
/// </summary>
/// <typeparam name="T">Tipo dos objetos armazenados.</typeparam>
public class SpatialHashGrid<T> : ISpatialIndex<T> where T : notnull {
	private readonly float _cellSize;
	private readonly Dictionary<CellKey, HashSet<T>> _cells;
	private readonly Dictionary<T, Bounds> _itemBounds;

	/// <summary>
	/// Cria uma nova grade hash espacial.
	/// </summary>
	/// <param name="cellSize">Tamanho de cada célula da grade.</param>
	public SpatialHashGrid(float cellSize) {
		if (cellSize <= 0) {
			throw new ArgumentException("Cell size must be greater than 0", nameof(cellSize));
		}

		_cellSize = cellSize;
		_cells = new Dictionary<CellKey, HashSet<T>>();
		_itemBounds = new Dictionary<T, Bounds>();
	}

	/// <summary>
	/// Tamanho de cada célula da grade.
	/// </summary>
	public float CellSize => _cellSize;

	/// <summary>
	/// Número total de objetos únicos indexados.
	/// </summary>
	public int Count => _itemBounds.Count;

	/// <summary>
	/// Insere um objeto na grade.
	/// </summary>
	public bool Insert(T item, Bounds bounds) {
		if (_itemBounds.ContainsKey(item)) {
			return false;
		}

		foreach (var cell in GetOverlappingCells(bounds)) {
			AddToCell(cell, item);
		}

		_itemBounds[item] = bounds;
		return true;
	}

	/// <summary>
	/// Consulta objetos dentro de uma área.
	/// </summary>
	public IReadOnlyList<T> Query(Bounds area) {
		var result = new List<T>();
		var seen = new HashSet<T>();

		foreach (var cell in GetOverlappingCells(area)) {
			if (!_cells.TryGetValue(cell, out HashSet<T>? itemsInCell)) {
				continue;
			}

			foreach (var item in itemsInCell) {
				if (!seen.Add(item)) {
					continue;
				}

				if (_itemBounds[item].Intersects(area)) {
					result.Add(item);
				}
			}
		}

		return result;
	}

	/// <summary>
	/// Atualiza os limites de um objeto na grade.
	/// </summary>
	public bool Update(T item, Bounds oldBounds, Bounds newBounds) {
		if (!_itemBounds.TryGetValue(item, out Bounds currentBounds)) {
			return false;
		}

		RemoveFromCells(item, currentBounds);

		foreach (var cell in GetOverlappingCells(newBounds)) {
			AddToCell(cell, item);
		}

		_itemBounds[item] = newBounds;
		return true;
	}

	/// <summary>
	/// Remove um objeto da grade.
	/// </summary>
	public bool Remove(T item) {
		if (!_itemBounds.TryGetValue(item, out Bounds bounds)) {
			return false;
		}

		RemoveFromCells(item, bounds);
		_itemBounds.Remove(item);
		return true;
	}

	/// <summary>
	/// Remove todos os objetos da grade.
	/// </summary>
	public void Clear() {
		_cells.Clear();
		_itemBounds.Clear();
	}

	private void AddToCell(CellKey cell, T item) {
		if (!_cells.TryGetValue(cell, out HashSet<T>? itemsInCell)) {
			itemsInCell = new HashSet<T>();
			_cells[cell] = itemsInCell;
		}

		itemsInCell.Add(item);
	}

	private void RemoveFromCells(T item, Bounds bounds) {
		foreach (var cell in GetOverlappingCells(bounds)) {
			if (!_cells.TryGetValue(cell, out HashSet<T>? itemsInCell)) {
				continue;
			}

			itemsInCell.Remove(item);
			if (itemsInCell.Count == 0) {
				_cells.Remove(cell);
			}
		}
	}

	private IEnumerable<CellKey> GetOverlappingCells(Bounds bounds) {
		int minX = ToCellCoordinate(bounds.MinX);
		int maxX = ToCellCoordinate(bounds.MaxX);
		int minY = ToCellCoordinate(bounds.MinY);
		int maxY = ToCellCoordinate(bounds.MaxY);

		for (int x = minX; x <= maxX; x++) {
			for (int y = minY; y <= maxY; y++) {
				yield return new CellKey(x, y);
			}
		}
	}

	private int ToCellCoordinate(float value) {
		return (int)MathF.Floor(value / _cellSize);
	}

	private readonly record struct CellKey(int X, int Y);
}
