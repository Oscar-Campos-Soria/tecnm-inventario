# Bajas y mantenimiento

## Dar de baja un equipo
La baja formal es la única forma de llevar un activo a DADO_DE_BAJA y es definitiva. Se hace en la ficha del activo,
sección "Baja formal", con **Dar de baja** (requiere el permiso de baja, ADMIN). Se captura el folio del acta
(obligatorio y único), el motivo (obsolescencia, daño irreparable, robo o extravío), el dictamen opcional y la fecha
(no futura). Antes de dar de baja, el equipo no debe tener incidentes abiertos ni una asignación vigente. Después se
puede descargar el acta en PDF para firmarla y subir el acta firmada como documento. Las bajas se consultan en el menú
**Bajas**.

## Registrar un mantenimiento
En la ficha del activo, sección "Mantenimiento", o en el menú **Mantenimiento** se registra una intervención
preventiva o correctiva ya realizada: fecha (no futura), descripción, piezas, costo y la fecha del próximo
preventivo. Un correctivo ligado a un incidente puede resolver ese incidente en el mismo paso. El mantenimiento no
cambia el estado del activo (eso lo hacen los incidentes). No se registra mantenimiento sobre equipos dados de baja.

## Programar un mantenimiento
Con **Programar** se agenda un mantenimiento a futuro (hasta 2 años) con fecha, tipo, descripción y responsable;
cuando se realiza se marca como realizado. En el menú **Mantenimiento** se ven los preventivos por vencer en 30, 60
o 90 días y los vencidos en rojo. Requiere el permiso de mantenimiento (ADMIN y TECNICO).

## Levantamiento físico (conteo de inventario)
En el menú **Levantamiento** (ADMIN y TECNICO) se abre un conteo por área: la lista esperada son los equipos de esa
ubicación y lo que contiene. Se escanea cada equipo con lector, teclado o cámara. Al cerrar, lo no escaneado queda como
NO_ENCONTRADO y las diferencias (en otro lugar, no encontrado, sobrante) se concilian una por una: registrar el
movimiento, ligar un incidente de robo o extravío, o dejar sin cambio. Nada cambia solo. Un conteo abierto se puede
cancelar con un motivo.
