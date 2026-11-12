# Activos

## Consultar activos
En el menú **Activos** está la lista de equipos con búsqueda por código de inventario, número de serie u hostname,
y filtros por estado, tipo y ubicación (al elegir un edificio o planta se incluye todo lo que contiene). Los equipos
dados de baja se ocultan; para verlos marca "incluir dados de baja". Al abrir un activo se ve su ficha: datos
generales, estado, ubicación, movimientos, asignaciones, mantenimiento, documentos y, según el rol, componentes y
software instalado.

## Dar de alta activos
Para registrar equipos nuevos (dar de alta activos) se usa el menú **Carga masiva**: descarga la plantilla, llénala (código de inventario y
tipo son obligatorios), súbela, revisa los errores fila por fila y confirma. Se crean todos o ninguno, en estado
DISPONIBLE. Si tu archivo tiene otro formato (otro Excel, un PDF o una foto de un oficio), usa la conversión con IA de
la misma pantalla. Requiere el permiso de crear activos (ADMIN y TECNICO).

## Estados de un activo
Un activo puede estar DISPONIBLE, ASIGNADO, EN_MANTENIMIENTO, CON_FALLA o DADO_DE_BAJA. Los estados cambian por lo que
pasa con el equipo: al reportar un incidente pasa a CON_FALLA; al iniciar la reparación, a EN_MANTENIMIENTO; al
resolver el último incidente abierto vuelve a DISPONIBLE (o ASIGNADO si tiene asignación vigente); al asignarlo pasa a
ASIGNADO y al devolverlo a DISPONIBLE. DADO_DE_BAJA es definitivo y solo se llega con la baja formal.

## Mover un activo de ubicación
En la ficha del activo, sección de movimientos, usa **Registrar movimiento** y elige la ubicación destino (debe ser
distinta a la actual). Solo se pueden mover activos DISPONIBLE o ASIGNADO. Cada movimiento queda en el historial y no
se borra; si te equivocaste, registra otro movimiento. Mover y asignar requieren permiso de ADMIN o TECNICO. Si alguien cambió el activo mientras lo veías, la pantalla
avisa y recarga.

## Asignar un activo a una persona
En la ficha del activo, sección de asignaciones, usa **Asignar** y elige al usuario (debe estar activo). Solo se puede
asignar un activo DISPONIBLE y sin otra asignación vigente: primero hay que devolver la asignación anterior. Para
devolverlo usa la opción de devolución en la asignación vigente; el activo vuelve a DISPONIBLE salvo que tenga
incidentes abiertos.

## Etiquetas con código QR
En la lista de activos marca las casillas de los equipos (se conservan entre páginas) y usa "Imprimir etiquetas
(PDF)": genera una hoja carta de 30 etiquetas con el QR del código de inventario, máximo 90 equipos por vez.
