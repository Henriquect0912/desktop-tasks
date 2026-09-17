# CLAUDE CODE — DESKTOPTASKS V0.2
## Evolução do V0.1: aplicativo principal + planejamento por dia

> **Base obrigatória:** esta versão parte da V0.1 já funcional.
> Preserve todos os comportamentos da V0.1 e implemente apenas o delta descrito aqui.
> Não reescreva o projeto do zero sem necessidade.
> Não antecipe features da V0.3 ou posteriores.

# 1. Objetivo da V0.2

A V0.1 responde: **“O que preciso fazer hoje?”**

A V0.2 acrescenta: **“Onde organizo o que quero fazer hoje, nesta semana e em algum momento futuro?”**

Separação central:
- **Widget:** execução rápida do dia.
- **Aplicativo principal:** organização e planejamento.

# 2. Escopo congelado

Implementar:
1. aplicativo principal separado do widget;
2. seções `Hoje`, `Esta semana` e `Algum dia`;
3. campo `PlannedDate` opcional nas tarefas;
4. criação rápida pelo widget;
5. criação completa pelo aplicativo;
6. tarefas planejadas para dias específicos;
7. tarefas sem data planejada em `Algum dia`;
8. indicação de tarefa planejada para data anterior;
9. drag-and-drop entre dias na visão semanal;
10. abertura do aplicativo principal a partir do widget;
11. persistência e migração do banco;
12. manutenção de todos os recursos da V0.1.

# 3. Fora do escopo

Não implementar:
- prazo/data limite;
- calendário mensal;
- recorrência;
- prioridades;
- categorias;
- provas;
- projetos;
- subtarefas;
- horários;
- duração;
- algoritmo de prioridade;
- IA;
- sincronização ou nuvem.

# 4. Novo conceito: data planejada

Adicionar à tarefa:

```text
PlannedDate: Date? nullable
```

Significado: dia em que o usuário pretende executar a tarefa.

Prazo ainda não existe nesta versão.

# 5. Aplicativo principal

Criar janela principal tradicional, separada do widget:

```text
┌──────────────────────────────────────────────────────┐
│ DesktopTasks                             + Nova tarefa│
├───────────────┬──────────────────────────────────────┤
│ Hoje          │                                      │
│ Esta semana   │ conteúdo da seção                   │
│ Algum dia     │                                      │
└───────────────┴──────────────────────────────────────┘
```

# 6. Seção Hoje

Mostrar:
- tarefas com `PlannedDate = hoje`;
- tarefas planejadas para datas anteriores e ainda pendentes.

Exemplo:

```text
☐ Fazer relatório
  planejada para ontem
```

Isso significa atraso no planejamento, não prazo vencido.

# 7. Seção Esta semana

Mostrar dias da semana atual e suas tarefas.

Permitir arrastar tarefa de um dia para outro.
Ao arrastar, atualizar e persistir `PlannedDate`.

# 8. Seção Algum dia

Mostrar tarefas com `PlannedDate = null`.

Permitir editar, excluir e concluir normalmente.

# 9. Criação rápida no widget

Ao clicar `+`, oferecer:

```text
Adicionar para hoje
Abrir aplicativo
```

## Adicionar para hoje
- permanece no widget;
- abre campo inline;
- `Enter` salva;
- `Esc` cancela;
- `PlannedDate = hoje`.

## Abrir aplicativo
- abre a janela principal;
- pode focar a área de nova tarefa.

# 10. Nova tarefa no aplicativo principal

Fluxo mínimo:

```text
O que precisa ser feito?
[____________________________]

Quando pretende fazer?

○ Hoje
○ Amanhã
○ Escolher dia
○ Algum dia

[Adicionar]
```

Regras:
- Hoje → `PlannedDate = hoje`;
- Amanhã → `PlannedDate = amanhã`;
- Escolher dia → seletor de data;
- Algum dia → `PlannedDate = null`.

# 11. Widget na V0.2

Continuar mostrando apenas:
- tarefas planejadas para hoje;
- tarefas atrasadas ainda pendentes;
- concluídas do dia atual.

Não mostrar a semana inteira.

# 12. Virada do dia

Tarefa pendente com data planejada passada não some.
Ela continua visível como atrasada no planejamento.

# 13. Banco de dados

Migrar `Tasks` adicionando `PlannedDate nullable`.

A migração deve ser não destrutiva e preservar tarefas da V0.1.
As tarefas antigas devem continuar visíveis após o upgrade.

# 14. Drag-and-drop

Na visão semanal:
- arrastar para outro dia;
- atualizar `PlannedDate`;
- refletir imediatamente na UI;
- refletir no widget se a nova data for hoje.

# 15. Critérios de aceite

- [ ] V0.1 continua funcionando;
- [ ] criação para hoje funciona sem abrir app;
- [ ] app principal abre pelo widget;
- [ ] Hoje funciona;
- [ ] Esta semana funciona;
- [ ] Algum dia funciona;
- [ ] drag entre dias altera `PlannedDate`;
- [ ] tarefa atrasada permanece visível;
- [ ] banco migra sem perda.

# 16. Testes mínimos

Testar:
1. tarefa de hoje;
2. tarefa futura;
3. tarefa atrasada pendente;
4. tarefa sem `PlannedDate`;
5. drag entre dias;
6. migração da V0.1;
7. conclusão segue a regra anterior.

# 17. Definition of Done

A V0.2 está pronta quando:
1. compila;
2. migra dados da V0.1;
3. widget permanece estável;
4. app principal funciona;
5. Hoje/Esta semana/Algum dia funcionam;
6. drag entre dias funciona;
7. criação rápida funciona;
8. testes passam;
9. README é atualizado.

**Não implementar recursos da V0.3.**
