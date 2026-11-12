# Incidentes y tickets

## Diferencia entre ticket e incidente
El **ticket** es la solicitud de ayuda de un usuario (mesa de ayuda): una falla, un problema de software, de cuenta,
de red o una solicitud de equipo; es la conversación con quien lo pidió. El **incidente** es el registro técnico de una
falla de un activo concreto y es el que cambia su estado. Un ticket nunca cambia el estado de un equipo: si resulta
ser una falla de hardware, el técnico levanta un incidente y lo liga al ticket.

## Reportar un problema o levantar un ticket
Cualquier usuario puede levantar un ticket en el menú **Tickets** con "Nuevo ticket": tipo, título, detalles,
urgencia (Normal, Alta o Urgente), el equipo si aplica y una foto o PDF opcional (máximo 5 MB). Desde la ficha de un
activo, "Reportar problema" abre el ticket con ese equipo ya elegido. El usuario ve y comenta solo sus propios
tickets.

## Estados de un ticket
Un ticket empieza ABIERTO; al asignarlo a un técnico pasa a EN_ATENCION; puede pasar a ESPERANDO_USUARIO cuando el
técnico necesita tu respuesta (vuelve a EN_ATENCION cuando comentas). El técnico lo marca RESUELTO; entonces el
solicitante confirma (CERRADO) o lo reabre si no quedó bien. Si nadie responde, se cierra solo a los 3 días de
resuelto. Mientras no esté resuelto, el solicitante o un técnico pueden cancelarlo (CANCELADO).

## Atender tickets (técnicos)
Los técnicos y administradores ven la bandeja completa en **Tickets** con pestañas Pendientes, Sin asignar, Asignados a
mí y Resueltos. Pueden asignar, cambiar la prioridad, pedir respuesta al usuario, escribir notas internas, resolver
(la resolución es obligatoria) y crear o ligar un incidente.

## Levantar un incidente
Los incidentes se registran contra un activo existente en el menú **Incidentes** (o desde un ticket) con tipo,
prioridad, título y descripción. Empiezan ABIERTO y el activo pasa a CON_FALLA. No se puede levantar un incidente
sobre un activo dado de baja.

## Estados de un incidente y cómo se resuelve
El flujo es ABIERTO, EN_DIAGNOSTICO, EN_REPARACION (el activo pasa a EN_MANTENIMIENTO) y RESUELTO; después CERRADO,
que es definitivo. Para resolver hay que escribir la solución (obligatoria) y opcionalmente el diagnóstico. Al
resolver el último incidente abierto de un activo, el activo vuelve a DISPONIBLE, o a ASIGNADO si tiene asignación
vigente. Solo ADMIN y TECNICO pueden atender y resolver incidentes.
