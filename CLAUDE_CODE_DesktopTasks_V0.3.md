# CLAUDE CODE — DESKTOPTASKS V0.3
## Evolução da V0.2: prazos + calendário mensal

> **Base obrigatória:** partir da V0.2 funcional.
> Preserve todos os recursos anteriores e implemente apenas o delta desta versão.
> Não antecipar recorrências da V0.4.

# 1. Objetivo da V0.3

A V0.2 organiza **quando pretendo fazer** uma tarefa.

A V0.3 acrescenta **até quando ela precisa ser feita** e **o que está chegando**.

Regra central:

> **Data planejada ≠ prazo.**

# 2. Escopo congelado

Implementar:
1. `DueDate` opcional;
2. separação entre data planejada e prazo;
3. calendário mensal por dias;
4. visualização de tarefas planejadas no calendário;
5. visualização de prazos no calendário;
6. seção `Próximos prazos`;
7. informação de prazo no widget quando relevante;
8. criação/edição de prazo;
9. migração do banco;
10. manutenção integral da V0.2.

# 3. Fora do escopo

Não implementar:
- horários;
- time blocking;
- eventos por hora;
- recorrência;
- rotinas;
- prioridades automáticas;
- categorias;
- provas especiais;
- projetos;
- subtarefas;
- IA;
- sincronização;
- Google Calendar ou Outlook.

# 4. Novo campo

Adicionar:

```text
DueDate: Date? nullable
```

Significado: data limite real.

Exemplo:

```text
Tarefa: Fazer relatório
PlannedDate = 2026-09-16
DueDate = 2026-09-18
```

# 5. Independência entre datas

Permitir:
- `PlannedDate != null`, `DueDate = null`;
- `PlannedDate = null`, `DueDate != null`;
- ambos preenchidos;
- ambos vazios.

# 6. Nova tarefa / edição

```text
O que precisa ser feito?
[ Fazer relatório ]

Quando pretende fazer?
[ 16/09/2026 ]   opcional

Prazo
[ 18/09/2026 ]   opcional

[Salvar]
```

# 7. Calendário mensal

Adicionar seção `Calendário`.

Visual focado em dias, não horários.

Distinguir:
- `•` item planejado;
- `!` prazo.

Manter aparência minimalista.

# 8. Próximos prazos

Adicionar lista:

```text
PRÓXIMOS PRAZOS

2 dias   Relatório de laboratório
5 dias   Lista de Termodinâmica
12 dias  Trabalho de Materiais
```

Ordenar por proximidade.

Excluir:
- tarefas concluídas;
- tarefas sem `DueDate`.

# 9. Prazo vencido

Se `DueDate < hoje` e tarefa pendente:

```text
prazo vencido
```

ou:

```text
vencido há 2 dias
```

Sem punição, gamificação ou alertas agressivos.

# 10. Widget

Mostrar prazo de forma discreta quando houver.

Exemplos:

```text
☐ Fazer relatório
  prazo em 2 dias
```

```text
☐ Lista 7
  prazo amanhã
```

Se também estiver atrasada no planejamento:

```text
☐ Fazer relatório
  planejada para ontem · prazo amanhã
```

# 11. Calendário: interação

No mínimo:
- navegar entre meses;
- clicar em dia e ver itens;
- identificar planejados;
- identificar prazos.

Opcional, se simples:
- arrastar tarefa planejada entre dias para mudar `PlannedDate`.

Não transformar em agenda horária.

# 12. Banco

Migrar `Tasks` adicionando `DueDate nullable`.

Migração não destrutiva.

# 13. Critérios de aceite

- [ ] V0.2 permanece funcional;
- [ ] `PlannedDate` e `DueDate` são independentes;
- [ ] prazo pode ser criado, alterado e removido;
- [ ] calendário mensal funciona;
- [ ] planejados aparecem no dia correto;
- [ ] prazos aparecem no dia correto;
- [ ] próximos prazos estão ordenados;
- [ ] concluídas não aparecem em próximos prazos;
- [ ] widget mostra prazo discretamente;
- [ ] vencidos são identificados;
- [ ] banco migra sem perda.

# 14. Testes mínimos

Testar:
1. prazo futuro;
2. prazo hoje;
3. prazo vencido;
4. concluída fora de próximos prazos;
5. datas planejada e limite diferentes;
6. posicionamento no calendário;
7. migração V0.2 → V0.3.

# 15. Definition of Done

A V0.3 está pronta quando:
1. compila;
2. V0.2 não regrediu;
3. prazo funciona;
4. calendário funciona;
5. próximos prazos funcionam;
6. widget continua simples;
7. banco migra corretamente;
8. testes passam;
9. README é atualizado.

**Não implementar rotinas ou recorrência nesta versão.**
