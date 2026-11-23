// Los estados del activo tal como se ven en pantalla: mismo color, símbolo y explicación en lista, ficha y "Mis equipos".
export interface AssetStatusView {
  chip: string
  fill: string
  // Qué significa y qué se puede hacer, en palabras de quien usa el sistema.
  help: string
}

const VIEWS: Record<string, AssetStatusView> = {
  DISPONIBLE: { chip: 'chip-success', fill: 'bg-state-available', help: 'Libre y funcionando. Se puede asignar a alguien o mover de lugar.' },
  ASIGNADO: { chip: 'chip-info', fill: 'bg-state-assigned', help: 'Alguien lo tiene a su cargo. Se puede mover; para dárselo a otra persona, primero regístralo como devuelto.' },
  EN_MANTENIMIENTO: { chip: 'chip-warning', fill: 'bg-state-maintenance', help: 'Está en reparación. No se puede mover ni asignar hasta que se resuelva.' },
  CON_FALLA: { chip: 'chip-danger', fill: 'bg-state-fault', help: 'Tiene una falla abierta. No se puede mover ni asignar hasta que se resuelva su incidente.' },
  DADO_DE_BAJA: { chip: 'chip-neutral', fill: 'bg-state-disposed', help: 'Salió del inventario de forma definitiva. Solo se consulta.' },
}

const UNKNOWN: AssetStatusView = { chip: 'chip-neutral', fill: 'bg-state-disposed', help: '' }

export function assetStatusView(code: string): AssetStatusView {
  return VIEWS[code] ?? UNKNOWN
}
