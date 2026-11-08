using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Tests;

/// <summary>Clasificación de un renglón del levantamiento físico.</summary>
public class InventoryRecordStatusTests
{
    private static readonly DateTime Checked = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false, true, 10L, 10L, InventoryRecordStatusCodes.Pendiente)]      // sin revisar
    [InlineData(true, true, 10L, 10L, InventoryRecordStatusCodes.Encontrado)]      // donde debía
    [InlineData(true, true, 10L, null, InventoryRecordStatusCodes.Encontrado)]     // encontrado sin ubicación capturada
    [InlineData(true, true, 10L, 11L, InventoryRecordStatusCodes.EnOtroLugar)]     // en otro salón
    [InlineData(true, false, 10L, null, InventoryRecordStatusCodes.NoEncontrado)]
    [InlineData(true, true, null, 11L, InventoryRecordStatusCodes.Sobrante)]       // no estaba en la lista
    public void Of_ClassifiesRecord(bool isChecked, bool found, long? expected, long? foundAt, string status)
        => Assert.Equal(status, InventoryRecordStatusCodes.Of(isChecked ? Checked : null, found, expected, foundAt));

    [Theory]
    [InlineData(InventoryRecordStatusCodes.Encontrado, false)]
    [InlineData(InventoryRecordStatusCodes.Pendiente, false)]
    [InlineData(InventoryRecordStatusCodes.EnOtroLugar, true)]
    [InlineData(InventoryRecordStatusCodes.NoEncontrado, true)]
    [InlineData(InventoryRecordStatusCodes.Sobrante, true)]
    public void NeedsAdjustment_OnlyDiscrepancies(string status, bool expected)
        => Assert.Equal(expected, InventoryRecordStatusCodes.NeedsAdjustment(status));
}
