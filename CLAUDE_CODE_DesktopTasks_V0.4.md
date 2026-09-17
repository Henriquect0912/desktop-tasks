# CLAUDE CODE — DESKTOPTASKS V0.4
## Evolução da V0.3: rotinas e recorrência

> **Base obrigatória:** partir da V0.3 funcional.
> Preserve todas as funcionalidades anteriores.
> Implementar apenas recorrências previsíveis.
> Não implementar planejamento automático de frequência flexível.

# 1. Objetivo da V0.4

Até a V0.3, uma tarefa comum é algo que faz uma vez e termina.

A V0.4 deve suportar ações que continuam existindo ao longo do tempo.

Princípio central:

> **Rotina é diferente de ocorrência.**

# 2. Escopo congelado

Implementar:
1. entidade `Routine`;
2. entidade `RoutineOccurrence`;
3. recorrência diária;
4. recorrência em dias específicos da semana;
5. recorrência a cada X dias;
6. recorrência mensal;
7. criação, edição e exclusão de rotina;
8. geração segura de ocorrências;
9. conclusão pelo widget;
10. próxima ocorrência conforme regra;
11. integração com Hoje/Semana/Calendário;
12. migração do banco;
13. preservação integral da V0.3.

# 3. Fora do escopo

Não implementar:
- “4 vezes por semana e o app escolhe os melhores dias”;
- reagendamento automático inteligente;
- algoritmo de carga;
- priorização automática;
- IA;
- streak;
- estatísticas;
- hábitos quantitativos;
- subtarefas;
- projetos;
- integrações externas.

# 4. Conceitos

## Tarefa
```text
Fazer relatório
```
Concluiu → acabou.

## Rotina
```text
Academia
seg / qua / sex / sáb
```
A rotina continua.

## Ocorrência
```text
Academia — 05/09/2026
```
Pode ser pendente ou concluída.

# 5. Modelo sugerido

## Routine

Campos mínimos sugeridos:

```text
Id
Title
RecurrenceType
StartDate
IntervalDays nullable
DaysOfWeek
DayOfMonth nullable
IsActive
CreatedAt
```

## RoutineOccurrence

```text
Id
RoutineId
OccurrenceDate
IsCompleted
CompletedAt
```

Adicionar constraint:

```text
UNIQUE(RoutineId, OccurrenceDate)
```

para impedir duplicatas.

# 6. Tipos de recorrência

Suportar:

## Todos os dias
```text
Revisar agenda
```

## Dias específicos
```text
Academia
SEG · QUA · SEX · SÁB
```

## A cada X dias
```text
Limpeza especial
A cada 15 dias
```

## Mensal
```text
Pagar conta
Todo dia 10
```

# 7. Dias específicos

Interface:

```text
[SEG] [TER] [QUA] [QUI] [SEX] [SÁB] [DOM]
```

Exigir ao menos um dia.

# 8. A cada X dias

```text
Repetir a cada:
[ 15 ] dias

Começando em:
[ 05/09/2026 ]
```

X deve ser inteiro positivo.

# 9. Mensal

```text
Todo dia [10] de cada mês
```

Regra para dia inexistente:
- se for dia 31 e o mês não tiver 31, usar o último dia do mês.

Documentar isso.

# 10. Criação

Ao clicar `+ Nova`, permitir:

```text
Tarefa
Rotina
```

Se `Tarefa`, usar fluxo existente.

Se `Rotina`:

```text
Nome
[ Academia ]

Repetição
○ Todos os dias
○ Dias específicos
○ A cada X dias
○ Mensal

[Salvar]
```

# 11. Widget

Ocorrência de rotina se comporta visualmente como tarefa normal:

```text
☐ Academia
```

Após concluir:

```text
☑ Academia
```

Ela:
- fica riscada;
- vai para baixo;
- permanece até virar o dia;
- não encerra a rotina.

# 12. Próxima ocorrência

Não transformar a ocorrência concluída na próxima.

Cada ocorrência deve ser independente.

# 13. Geração de ocorrências

Implementar de forma idempotente.

Nunca criar duplicatas.

Recomendação:
- gerar sob demanda ou para horizonte limitado de 60–90 dias;
- regenerar conforme necessário;
- usar constraint única.

Não gerar anos infinitos de ocorrências.

# 14. Hoje

Misturar:
- tarefas comuns;
- ocorrências de rotina.

Pode usar ícone discreto `↻`, sem poluição visual.

# 15. Esta semana

Mostrar ocorrências junto das tarefas planejadas.

# 16. Calendário

Mostrar ocorrências nos dias corretos.

Exibir ocorrências, não a definição abstrata da rotina.

# 17. Editar rotina

Ao alterar regra:
- manter passado;
- remover/recalcular ocorrências futuras ainda não concluídas;
- gerar novas conforme regra atualizada.

# 18. Excluir rotina

Confirmar exclusão.

Ao excluir:
- remover/desativar rotina;
- remover futuras pendentes;
- preservar integridade referencial.

# 19. Conclusão de ocorrência

```text
IsCompleted = true
CompletedAt = agora
```

Não alterar a rotina para “concluída”.

# 20. Virada do dia

Ocorrência concluída ontem deixa de aparecer em Hoje.
Próximas ocorrências continuam conforme regra.

# 21. Recorrência flexível fica para depois

Não implementar:

```text
Academia 4x por semana
```

com escolha automática dos dias.

Nesta versão os dias/regras são explícitos.

# 22. Banco

Criar tabelas novas:

```text
Routines
RoutineOccurrences
```

Usar foreign keys e constraints.

Não destruir `Tasks`.

# 23. Migração

V0.3 → V0.4:
- preservar tarefas;
- preservar `PlannedDate`;
- preservar `DueDate`;
- preservar settings/layouts;
- adicionar tabelas de rotina.

Não converter tarefas existentes automaticamente em rotina.

# 24. Critérios de aceite

- [ ] V0.3 continua funcionando;
- [ ] rotina diária funciona;
- [ ] dias específicos funcionam;
- [ ] a cada X dias funciona;
- [ ] mensal funciona;
- [ ] ocorrências corretas são geradas;
- [ ] não há duplicatas;
- [ ] ocorrência aparece em Hoje;
- [ ] aparece na Semana;
- [ ] aparece no Calendário;
- [ ] conclusão não encerra a rotina;
- [ ] próxima ocorrência aparece;
- [ ] edição recalcula futuras;
- [ ] exclusão trata futuras;
- [ ] banco migra sem perda.

# 25. Testes mínimos

Testar:
1. diária;
2. dias específicos;
3. a cada X dias;
4. mensal;
5. fevereiro com dia 30/31;
6. ano bissexto;
7. duplicidade;
8. conclusão;
9. continuidade da rotina;
10. edição;
11. exclusão;
12. migração V0.3 → V0.4.

# 26. Definition of Done

A V0.4 está pronta quando:
1. compila;
2. recursos anteriores permanecem;
3. quatro tipos de recorrência funcionam;
4. ocorrências são idempotentes;
5. integração com widget/Hoje/Semana/Calendário funciona;
6. migração é segura;
7. testes passam;
8. README é atualizado.

**Não implementar frequência flexível ou planejamento automático.**
