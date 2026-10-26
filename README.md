# DesktopTasks — V0.7.1

Aplicativo pessoal de tarefas para Windows, em duas peças que fazem coisas
diferentes:

- **Widget** — fica na área de trabalho e responde *"o que eu preciso fazer
  hoje?"*. É só execução do dia.
- **Aplicativo principal** — janela normal, responde *"onde eu organizo o que
  quero fazer hoje, nesta semana e algum dia?"*. É planejamento: tarefas,
  projetos, entregas, provas, rotinas, capacidade e o plano do dia.

Dados 100% locais em SQLite, sem conta, sem rede, sem telemetria. (O atalho
para o Claude só pede ao Windows para abrir um endereço no navegador; o
aplicativo em si não acessa a rede.)

## O que cada versão acrescentou

| Versão | O que entrou | Especificação |
| --- | --- | --- |
| **V0.1** | O widget: tarefas do dia, concluir, reordenar, dois temas, bandeja, atalho global, multi-monitor | [ESPECIFICACAO_V0.1.md](ESPECIFICACAO_V0.1.md) |
| **V0.2** | Aplicativo principal, `PlannedDate`, seções Hoje / Esta semana / Algum dia, arrastar entre dias | [CLAUDE_CODE_DesktopTasks_V0.2.md](CLAUDE_CODE_DesktopTasks_V0.2.md) |
| **V0.3** | `DueDate` (prazo), calendário mensal, Próximos prazos, prazo no widget | [CLAUDE_CODE_DesktopTasks_V0.3.md](CLAUDE_CODE_DesktopTasks_V0.3.md) |
| **V0.4** | Rotinas e recorrência (diária, dias específicos, a cada X dias, mensal) | [CLAUDE_CODE_DesktopTasks_V0.4.md](CLAUDE_CODE_DesktopTasks_V0.4.md) |
| **V0.5** | Projetos, Entregas, subtarefas de um nível, progresso e distribuição automática | [CLAUDE_CODE_DesktopTasks_V0.5.md](CLAUDE_CODE_DesktopTasks_V0.5.md) |
| **V0.6** | Provas: conteúdos, níveis de domínio, listas de exercícios, sessões e plano de estudo | [CLAUDE_CODE_DesktopTasks_V0.6.md](CLAUDE_CODE_DesktopTasks_V0.6.md) |
| **V0.7** | Motor de planejamento: prioridade, duração, capacidade diária, sobrecarga e plano do dia explicado | [CLAUDE_CODE_DesktopTasks_V0.7.md](CLAUDE_CODE_DesktopTasks_V0.7.md) |
| **V0.7.1** | Provas refeitas: árvore de estudo, grafo de dependências e limites humanos de estudo por prova | [CLAUDE_CODE_DesktopTasks_V0.7.1_ExamGraph_Update.md](CLAUDE_CODE_DesktopTasks_V0.7.1_ExamGraph_Update.md) |

Como cada especificação manda evoluir a anterior sem reescrever nada, tudo vive
numa **única solução**, dentro da pasta `V0.1` (onde as sete especificações
estão). O banco migra sozinho de qualquer versão para a atual, sem perder dados.

A V0.7.1 não é um ajuste visual: ela corrige dois erros de modelo que só
apareceram no uso real — dependências entre conteúdos que o planejador ignorava,
e a capacidade geral do dia sendo confundida com o quanto dá para estudar de uma
matéria só.

Depois dela, ajustes pedidos no uso do dia a dia mudaram regras que as
especificações tinham fixado. Estão registrados aqui porque contrariam o texto
delas de propósito:

| Antes | Agora | Por quê |
| --- | --- | --- |
| V0.7 §16: o que ficou para trás só mudava de dia com `[Recalcular]` | vem para hoje sozinho, na abertura e na virada do dia | pendência não expira desde a V0.1; a data velha só obrigava a arrastar tudo à mão todo dia |
| V0.7.1 §19–20: item com pré-requisito pendente ficava fora do plano | ele é planejado normalmente, só nunca antes do que vem antes dele | a dependência era para **ordenar**, não para trancar |
| V0.7.1 §30: o plano de estudo começava hoje e antecipava | ele começa o mais tarde possível ainda no ritmo confortável | ninguém começa hoje a estudar para uma prova daqui a dois meses |
| V0.5 §4: a distribuição de projetos e entregas antecipava | ela encosta o trabalho no prazo e anuncia a data-limite para começar | é a mesma regra da prova; não fazia sentido valer só para um lado |
| V0.2 §11: o widget mostrava apenas o que é de hoje | o terço de baixo mostra os **lembretes** — as tarefas sem data | o que não tem dia precisava ficar à vista, e não escondido no aplicativo |
| V0.1 §17: nada de inteligência artificial nem integração com outro serviço; V0.2 §9: o `+` tinha duas opções | o `+` ganhou **Pedir ideias ao Claude ↗**, que abre uma conversa nova no navegador | um lugar à mão para pedir ideias do que aprender. O aplicativo continua sem acessar a rede: quem abre o endereço é o Windows, e nada do aplicativo vai junto |

Os conceitos de data são independentes e não se confundem:

| Conceito | Significa | Onde aparece |
| --- | --- | --- |
| **Data planejada** | quando você *pretende* fazer | Hoje, Semana, Calendário, Plano do dia, widget |
| **Prazo** | até quando *precisa* estar feito | Próximos prazos, Calendário, widget |
| **Rotina** | algo que se *repete* e não termina | Hoje, Semana, Calendário, widget |
| **Data da prova** | o prazo de uma preparação inteira | Provas, Calendário, sessões em Hoje |

E os tipos de item, que também são coisas diferentes:

| Item | O que é | Conclui? |
| --- | --- | --- |
| **Tarefa** | uma coisa a fazer | sim |
| **Subtarefa** | uma etapa dentro de um projeto ou entrega | sim |
| **Projeto** | iniciativa maior, várias etapas ao longo do tempo | só quando você diz |
| **Entrega** | escopo menor, poucos passos, resultado definido | só quando você diz |
| **Rotina** | uma regra que se repete | nunca — quem conclui é a ocorrência do dia |
| **Prova** | disciplina, data e o que cai nela | nunca — quem conclui são as sessões |

---

## 1. Requisitos suportados

- Windows 10 ou Windows 11, 64 bits.
- Não exige privilégios de administrador.
- Não exige o SDK do .NET instalado (a publicação é self-contained).
- DPI por monitor (`PerMonitorV2`) declarado no manifesto.
- Funciona sem internet.

---

## 2. Como usar

### 2.1 Onde está o executável

Publicação self-contained, single-file, `win-x64`:

```text
V0.1\src\HojeWidget\bin\Release\net8.0-windows\win-x64\publish\HojeWidget.exe
```

Uma cópia instalada fora do OneDrive fica em:

```text
%LOCALAPPDATA%\Programs\HojeWidget\HojeWidget.exe
```

> **Recomendado usar a cópia em `%LOCALAPPDATA%\Programs`.** A pasta `publish`
> está dentro do OneDrive e o executável tem ~130 MB: deixá-lo ali faz o OneDrive
> sincronizar o arquivo inteiro a cada nova publicação.

Junto do `.exe` ficam algumas DLLs nativas (`wpfgfx_cor3.dll`,
`PresentationNative_cor3.dll`, `D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll`,
`vcruntime140_cor3.dll`, `e_sqlite3.dll`). O modo single-file continua ativo; o
SDK do .NET mantém essas bibliotecas nativas ao lado do executável por padrão.
**Copie a pasta inteira, não só o `.exe`.**

### 2.2 Iniciar e parar

| Ação | Como fazer |
| --- | --- |
| Iniciar | Atalho **`Hoje Widget`** no menu Iniciar ou na área de trabalho. O widget aparece na área de trabalho e um ícone fica perto do relógio. |
| Ocultar / mostrar | Clique simples no ícone da bandeja, ou `Mostrar` / `Ocultar` no menu do botão direito. |
| Trazer para a frente | `Ctrl + Shift + Q`. Aperte de novo para devolver à área de trabalho. |
| Encerrar de verdade | Botão direito no ícone da bandeja → `Sair`. |

Ocultar **não** encerra o processo. Só `Sair` encerra.

Abrir o aplicativo uma segunda vez não cria um segundo widget: a nova execução
avisa a instância existente e fecha.

### 2.3 O widget — execução do dia

O widget tem duas listas, separadas por uma linha fina:

```text
HOJE                               +     ┐
○ ↻ Academia                             │ dois terços
○ Fazer relatório                        │
──────────────────────────────────       ┘
LEMBRETES                          +     ┐ um terço
○ Renovar passaporte                     ┘
1 pendente · 1 lembrete
```

**Hoje**, nos dois terços de cima, é o que é para hoje: tarefas planejadas para
hoje, pendências de dias anteriores, ocorrências de rotina e sessões de estudo
do dia, e o que você concluiu hoje. Tarefas planejadas para outros dias não
aparecem aqui — elas vivem no aplicativo principal.

**Lembretes**, no terço de baixo, é o que você tem de fazer mas não tem dia:
"renovar o passaporte", "comprar pilha". São as mesmas tarefas da seção
**Algum dia** do aplicativo principal — um conceito só, duas vistas. Criar um
lembrete no widget faz ele aparecer em Algum dia, e vice-versa.

- O `+` de `LEMBRETES` troca o título por um campo. `Enter` salva e deixa o
  campo aberto para o próximo; `Esc` fecha. O lembrete nasce sem data.
- A bolinha, o `⋯ → Editar / Excluir` e o riscado até a virada do dia funcionam
  como em Hoje. Lembretes não se reordenam por arrasto.
- Riscar um lembrete **não** conta como "Tudo concluído por hoje": ele nunca foi
  de hoje.
- Um lembrete não é trazido para hoje na virada do dia — ele nunca ficou para
  trás, porque nunca teve dia. Para fazê-lo num dia, dê uma data a ele no
  aplicativo.
- Etapas de projeto sem data não viram lembrete: elas já têm lugar no projeto.
- O rodapé conta os dois: `1 pendente · 2 lembretes`. Sem lembrete pendente, ele
  fica como sempre foi.

- `+` no canto superior direito abre três opções:
  - **Adicionar para hoje** — campo dentro do próprio widget, `Enter` salva,
    `Esc` cancela. A tarefa nasce planejada para hoje.
  - **Abrir aplicativo** — abre a janela de planejamento.
  - **Pedir ideias ao Claude ↗** — abre `https://claude.ai/new`, uma conversa
    nova, no navegador padrão. A seta avisa que sai do aplicativo. O endereço é
    sempre o mesmo e não leva nada junto: nenhuma tarefa, prova ou texto seu vai
    para o Claude sem que você mesmo escreva lá.
- Clique na bolinha para concluir. A tarefa fica riscada, atenuada e vai para o
  fim da lista. Clicar de novo no mesmo dia devolve a tarefa ao lugar de antes.
- Passe o mouse sobre uma tarefa e clique em `⋯` para `Editar` ou `Excluir`.
  Excluir pede confirmação e é definitivo — não há lixeira nem desfazer.
- Arraste uma tarefa pendente para reordenar. A ordem é salva na hora.
- Arraste o cabeçalho `HOJE` para mover o widget. Arraste as bordas e os cantos
  para redimensionar (a borda é invisível, mas está lá).

Abaixo do título aparece uma linha discreta quando há algo a dizer:

```text
☐ Fazer relatório
  planejada para ontem · prazo amanhã
```

Ocorrências de rotina aparecem com `↻` antes do nome e se comportam como tarefa
normal — mas não têm menu `⋯`: quem se edita é a rotina, no aplicativo principal.

### 2.4 O aplicativo principal — planejamento

Abre pelo `+` do widget → **Abrir aplicativo**, ou pelo menu da bandeja →
**Abrir aplicativo**. Fechar a janela **não** encerra o aplicativo: ela só some.

| Seção | O que mostra |
| --- | --- |
| **Hoje** | o mesmo que o widget: planejadas para hoje, atrasadas no planejamento e rotinas do dia |
| **Esta semana** | os sete dias da semana em linhas. **Arraste uma tarefa de um dia para outro** para mudar a data planejada |
| **Algum dia** | tarefas sem data planejada — o que você quer fazer, mas ainda não marcou quando |
| **Calendário** | o mês em dias, com `•` para item planejado, `!` para prazo e `◆` para prova. Clique num dia para ver o que há nele |
| **Próximos prazos** | tarefas com prazo, ordenadas por proximidade. Concluídas e sem prazo ficam de fora |
| **Rotinas** | as rotinas cadastradas e sua regra, com editar e excluir |
| **Projetos** | iniciativas maiores, com etapas, progresso e distribuição automática |
| **Entregas** | escopos menores, mesma mecânica, conceito separado |
| **Provas** | disciplina, data, conteúdos, listas, domínio e plano de estudo |
| **Plano do dia** | a ordem sugerida para hoje, com o porquê de cada item, a carga contra a capacidade e o aviso de sobrecarga |
| **Capacidade** | quanto tempo cada dia da semana costuma ter, e exceções por data |

A navegação é agrupada em **Dia a dia** (Hoje, Esta semana, Algum dia),
**Planejamento** (Plano do dia, Calendário, Próximos prazos, Capacidade) e
**Estruturas** (Projetos, Entregas, Provas, Rotinas).

**Nova tarefa** (`+ Nova tarefa`, no topo): texto, *quando pretende fazer*
(Hoje / Amanhã / Escolher dia / Algum dia), *prazo* (opcional e independente do
planejamento), *duração estimada*, se ela *pode ser dividida em sessões* e a
*prioridade*. O mesmo formulário abre em `⋯ → Editar`.

O campo de duração aceita `1h30`, `90` ou `1:30`. **Deixar em branco é uma
resposta válida**: o aplicativo prefere admitir que não sabe quanto tempo algo
leva a inventar um número — e o planejamento lida com isso.

`⋯ → Mover para…` coloca uma tarefa que já existe dentro de um projeto ou
entrega. Texto, conclusão, datas, duração e ordem continuam exatamente como
estavam; a única coisa que muda é a quem ela pertence.

**Nova rotina** (seção Rotinas → `+ Nova rotina`): nome e a regra de repetição —
todos os dias, dias específicos da semana, a cada X dias, ou todo dia N do mês.

### 2.5 Projetos e entregas

Um **projeto** é uma iniciativa maior, com várias etapas ao longo do tempo
("Preparação para mestrado"). Uma **entrega** tem poucos passos e um resultado
final definido ("Relatório de laboratório, prazo 15/09"). Por dentro os dois
usam a mesma mecânica; na tela continuam sendo duas seções separadas, porque
para você eles são coisas diferentes.

Cada um tem **subtarefas de um nível só** — uma etapa nunca vira mãe de outra
etapa. Isso não é uma regra escrita à mão em algum lugar: a subtarefa é uma
tarefa cujo pai é o projeto, e o pai nunca pode ser outra tarefa.

O progresso é a conta mais simples possível, `concluídas / total`:

```text
3 de 7 concluídas
████░░░░░░ 43%
```

Ele aparece na seção do projeto e também embaixo da subtarefa em Hoje e na
Semana (`Preparação para mestrado · 3 de 7`).

Ao marcar a **última** etapa, aparece a pergunta:

```text
Todas as etapas foram concluídas.
O projeto "X" está concluído?
[ Sim, concluir ]  [ Ainda não ]
```

Nunca é automático. Um projeto pode ter todas as etapas feitas e ainda não estar
acabado, e só você sabe disso.

**Excluir** um projeto não apaga trabalho: as subtarefas voltam a ser tarefas
soltas, com data, conclusão e ordem intactas.

#### Distribuir automaticamente

Com um prazo definido, `Distribuir automaticamente` propõe um dia para cada
etapa pendente. As regras, na ordem em que valem:

1. trabalha do dia de hoje até o prazo;
2. pula os dias com capacidade 0;
3. guarda o último dia útil antes do prazo como **margem de segurança**;
4. **encosta o trabalho no prazo**: ocupa os últimos dias possíveis, e o primeiro
   deles é a data-limite para começar;
5. continua espalhando — uma etapa por dia enquanto houver dia —, só que ancorado
   no fim, e sem inverter a ordem das etapas;
6. divide em sessões o que pode ser dividido, e em **poucas** sessões: quatro
   horas com capacidade de duas viram dois dias de duas horas, não cinco pedaços;
7. procura um dia inteiro livre para o que **não** pode ser dividido — e informa
   conflito, em texto, quando nenhum dia serve.

O resultado é uma proposta, nunca uma gravação:

```text
PLANO SUGERIDO
Relatório de laboratório · Prazo: 16/09 · 8 dias

  Comece em 14/09. Esse é o limite: começando depois
  disso, o cronograma aperta.

14/09  Organizar dados
       45 min
15/09 — Margem de segurança

[ Cancelar ]  [ Ajustar ]  [ Aceitar ]
```

A data-limite é a recomendação principal, e você **não escolhe dia nenhum**: as
datas aparecem como texto. `Ajustar` libera cada linha para mudar antes de
aceitar. **Nada é aplicado sem o seu aceite** — não existe redistribuição
silenciosa.

### 2.6 Provas

Uma prova tem disciplina, data, **limites de estudo** e uma **árvore** do que cai
nela. Ela não é uma tarefa e não se conclui; o que entra em Hoje e na Semana são
as **sessões de estudo**.

#### A árvore e o grafo são coisas diferentes

Na tela, a prova é uma árvore de assuntos:

```text
PROVA — MPP-31
Data: 10/09        Meta 2h/dia · máximo 3h/dia · sessão até 1h30

▼ Torção
   ○ Teoria                          1h
   🔒 Lista 1        10 questões · 1h    Requer: Teoria

▼ Flexão Bi-Axial
   ○ Teoria                          1h
   🔒 Lista 2         8 questões · 45 min  Requer: Teoria

▼ Paredes Finas
   ○ Teoria                          2h
   🔒 Lista 3        15 questões · 3h    Requer: Teoria, Lista 2
```

Por baixo, as dependências formam um **grafo dirigido acíclico**. São duas
estruturas sobre os mesmos itens, e elas não se misturam:

- a **árvore** (`+ Assunto`, e dentro dele `+ Teoria`, `+ Lista`, `+ Outro`) é só
  organização — é como você agrupa o que cai na prova;
- o **grafo** manda na ordem de execução, e um item de um ramo pode depender de
  um item de outro ramo, como a Lista 3 acima.

**Mover um item de assunto não cria nem apaga dependência nenhuma.** As duas
coisas se editam em lugares diferentes de propósito.

Cada assunto acrescenta filhos ali mesmo, na própria linha: nada de abrir uma
janela por conteúdo. Clicar num item abre o painel embaixo da árvore, com
duração, questões, divisibilidade e a lista de dependências.

#### Dependência ordena, não tranca

Um item com pré-requisito pendente **continua sendo planejado e continua podendo
ser feito a qualquer momento**. O que a dependência faz é dizer a ordem: o
planejador nunca coloca a Lista 3 antes de a Lista 2 terminar.

O estado nunca depende só de cor:

```text
○ pendente     ✓ concluído     ↳ vem depois de outro     ◐ parcial
```

`◐ parcial` é o item que tem sessões, algumas feitas e outras não. Concluir a
primeira de duas sessões **não** conclui o item.

Para efeito de ordem, só a conclusão de verdade conta. Não contam: ter data
planejada, ter sessão criada, a data já ter passado, ou uma das sessões já ter
sido feita.

Ao criar uma lista dentro de um assunto que já tem teoria, a caixa
`Esta lista depende da teoria deste assunto` vem marcada. Você pode desmarcar
antes de criar, ou remover a dependência depois no painel do item.

Dependências que quebrariam o grafo são recusadas com o motivo escrito: depender
de si mesmo, repetir uma dependência que já existe, ou fechar um ciclo — que
deixaria todos os envolvidos travados para sempre, e em silêncio.

#### Limites de estudo: três números diferentes

Ao criar a prova, três limites aparecem explicitamente — nunca como padrão
invisível:

| Limite | O que é | Como o planejador trata |
| --- | --- | --- |
| **Meta confortável por dia** | o ritmo que você quer manter | preferência: ele tenta ficar por perto |
| **Máximo desta prova por dia** | o teto da matéria | **limite duro**: nunca é ultrapassado sozinho |
| **Máximo de uma sessão contínua** | o maior bloco de uma vez | **limite duro** por sessão |

Isso é **coisa diferente da capacidade geral do dia**. Um sábado com 8h livres
comporta um plano assim:

```text
MPP-31         3h      ← o máximo da matéria, não das 8h
Relatório      2h
Outra matéria  2h
Academia       1h
```

Mesmo com 24h de capacidade geral, MPP-31 continua limitada ao seu máximo
diário — e o dia **não** é preenchido à força com mais da mesma prova. A
capacidade que sobra fica para outra matéria, um projeto, uma tarefa, uma rotina,
ou simplesmente livre.

#### A data-limite para começar

O plano **não começa hoje só porque hoje existe**. Ele é calculado de trás para
frente: ocupa os últimos dias necessários no ritmo confortável, e o primeiro
deles é a recomendação principal:

```text
Comece em 14/09. Esse é o limite: começando depois disso, o cronograma aperta.
```

É literalmente isso: um dia depois dessa data, o mesmo conteúdo já não cabe na
meta confortável e começa a empurrar os dias para o máximo diário.

A janela não sai só da conta dos minutos. Ela começa por essa conta e **recua um
dia por vez** até o plano caber de verdade, porque a conta ignora três coisas:
uma sessão tem tamanho máximo, a sobra de meia hora de um dia não comporta uma
sessão inteira, e um item só começa depois daquilo de que depende. Quando nem a
janela inteira cabe no ritmo confortável, o plano sobe para o máximo diário — e,
mesmo assim, continua começando o mais tarde que ainda cabe.

Um item que não pode ser dividido e é maior que o máximo por sessão não cabe em
janela nenhuma: ele aparece como conflito e não arrasta o resto do plano para
hoje.

Você **não escolhe data nenhuma**. As datas aparecem só como texto; se quiser
mexer numa delas, `Ajustar datas` libera os campos.

Quando o conteúdo não cabe no ritmo confortável, o plano abre a janela inteira e
sobe até o máximo diário — e diz isso:

```text
O conteúdo não cabe na meta confortável de 2h por dia;
alguns dias vão até o máximo de 3h.
```

#### Gerar plano de estudo

`Gerar plano de estudo` produz uma proposta com **tudo** o que falta. Um item que
espera a vez de outro entra no plano do mesmo jeito, colocado depois dele, e
aparece também listado à parte com a ordem explicada:

```text
↳ Lista 3 — Vem depois de Teoria, Lista 2.
```

Quando o tempo do dia é curto, a vez é de quem vem antes: com uma hora livre e a
Lista 3 dependendo da Lista 2, a hora vai para a Lista 2.

Cada sessão diz por que foi escolhida:

```text
07/09   Flexão Bi-Axial — teoria        1h
        Escolhida porque ainda não estudada · prepara Lista 2.

08/09   Lista 3 (1/2)                   1h30
        Escolhida porque colocada depois de Lista 2 · sessão limitada a 1h30 pelo máximo contínuo.
```

Como provas costumam ser de manhã, o conteúdo principal termina até **D-2** e a
véspera fica para revisão. Ela só recebe conteúdo novo quando o resto não coube,
e isso aparece escrito.

Quando o trabalho não cabe nos limites, o aplicativo não finge que cabe:

```text
⚠ Não existe um plano viável com as restrições atuais.
Trabalho restante: 10h · capacidade possível até a prova: 6h · déficit: 4h

[ Aumentar limite temporariamente ]  [ Ajustar conteúdos ]
[ Ajustar estimativas ]              [ Manter plano incompleto ]
```

`Aumentar limite temporariamente` recalcula **uma vez** com o teto elevado, e
diz isso no cabeçalho. A política gravada da prova não muda: para alterá-la de
verdade é preciso ir no formulário.

Se você apertar um limite com um plano já feito, as sessões que passam do novo
limite ficam **marcadas para revisão** — nada é corrigido nem apagado em
silêncio. Mudar uma dependência **não** invalida sessão nenhuma: a ordem nova
vale para o próximo plano, e o que já estava marcado continua valendo.

Sessão que não foi feita no dia vem para hoje sozinha, como qualquer outra
pendência.

#### A prova na agenda

A prova aparece na agenda como **lembrete, nunca como tarefa**: não tem caixa
para marcar, não se conclui e não some quando o dia passa.

- no **Calendário**, o dia da prova ganha um `◆` âmbar, diferente do `•` de item
  planejado e do `!` de prazo;
- ao clicar naquele dia, ela aparece no topo do painel, em destaque, com
  "Prova neste dia";
- em **Próximos prazos**, ela vem antes dos prazos, sob o título `PROVAS`.

### 2.7 Plano do dia e capacidade

A seção **Capacidade** diz quanto tempo cada dia da semana costuma ter. Um dia
com **0** não recebe planejamento automático — e você continua livre para pôr
algo lá à mão. Uma **exceção por data** sobrescreve um único dia.

O padrão é 3h em todos os dias. Zerar um dia é uma decisão sua; o aplicativo não
zera nada sozinho.

A seção **Plano do dia** responde *"o que faz mais sentido eu fazer hoje?"*:

```text
HOJE — PLANO SUGERIDO                              [ Redistribuir ]
5 atividades · 3h50 estimadas
Capacidade de hoje: 3h

1. MPP-31 — estudar Lista 1        prova em 4 dias · conteúdo não estudado   1h30
2. Fazer relatório                 prazo amanhã · prioridade alta            1h30
3. Academia                        rotina do dia                       sem estimativa
```

Cada item diz **por que** está ali. O motor tem uma nota interna, mas ela é um
detalhe de implementação: o que aparece são os motivos, em português.

Os fatores considerados são prazo e sua proximidade, atraso no planejamento,
prioridade manual, data da prova, domínio fraco ou não estudado, duração,
capacidade do dia, margem de segurança, tipo do item, se ele pode ser dividido e
se você já escolheu aquele dia à mão.

Quando a carga passa da capacidade:

```text
⚠ Dia sobrecarregado
Carga planejada: 7h30 · Capacidade: 3h        [ Redistribuir ]
```

`Redistribuir` **propõe** mudanças de dia — e só isso. Ele não toca no que você
planejou à mão, não usa dia de capacidade 0, não passa do prazo do item, não
invade a margem e não quebra o que não pode ser quebrado. Você vê a lista de
mudanças e decide entre `Manter como está` e `Aplicar`.

#### O que ficou para trás vem sozinho

Pendência não expira. O que estava planejado para antes e não foi concluído
**vem para hoje automaticamente**, na abertura do aplicativo e a cada virada de
dia — tarefas, subtarefas e sessões de estudo. Você não precisa arrastar nada.

O Plano do dia diz que isso aconteceu, para a lista não mudar sem explicação:

```text
2 atividades que não foram concluídas antes vieram para hoje.
```

Ocorrência de rotina é a exceção: a de ontem é de ontem. Quem manda no dia dela é
a regra da rotina, e trazê-la duplicaria a de hoje.

Nada é apagado nem concluído nesse processo — só muda o dia. A marca de "você
escolheu este dia" é preservada: a decisão continua sendo sua, ela só passou a
valer para hoje.

Se o trabalho simplesmente não cabe antes dos prazos, o aplicativo diz isso com
os números na mão, em vez de fingir que o plano é viável:

```text
Até 12/09 a carga necessária é de 11h e a capacidade disponível é de 7h.
```

Quando tudo do dia está feito, aparece `✓ Tudo concluído por hoje.` e um botão
`Ver amanhã`. **Ver, não puxar**: o aplicativo nunca traz trabalho de amanhã
para hoje por conta própria.

### 2.8 Rotinas: a regra e o dia são coisas diferentes

Uma **rotina** é a regra ("Academia, seg · qua · sex · sáb"). Ela não é
concluída e não termina. O que se conclui é a **ocorrência** de um dia
("Academia — 05/09/2026").

Concluir a ocorrência de hoje risca aquela linha e a manda para baixo, exatamente
como uma tarefa — e não mexe na rotina: a ocorrência de amanhã continua lá,
pendente. Cada ocorrência é independente das outras.

As ocorrências são geradas com antecedência de 90 dias e recriadas conforme
necessário. A geração é idempotente — pode rodar quantas vezes for, nunca
duplica —, garantida por uma constraint única em `(routine_id, occurrence_date)`.

Ao **editar** uma rotina, o passado é mantido intacto e só as ocorrências futuras
ainda pendentes são recalculadas pela nova regra. Ao **excluir**, a rotina e suas
ocorrências saem juntas, com confirmação.

Regra mensal para dia inexistente: **dia 31 num mês que não tem 31 usa o último
dia daquele mês** — 30 de abril, 28 de fevereiro (29 em ano bissexto).

### 2.9 Regra do dia

Pendência não expira: uma tarefa não concluída continua aparecendo
indefinidamente. Conclusão expira visualmente: tarefas concluídas somem na
virada da data local, sem precisar reiniciar o aplicativo.

Uma tarefa planejada para uma data que já passou **não some**: ela continua em
Hoje, marcada como `planejada para ontem`. Isso é atraso de planejamento, não
prazo vencido — são coisas diferentes e o aplicativo não as mistura.

Ocorrência de rotina segue a mesma lógica pela data da ocorrência: a de ontem
sai de Hoje na virada do dia, concluída ou não, e a de hoje entra.

Na primeira vez que o widget é mostrado em cada data, aparece a revisão diária.
Desde a V0.7 ela diz o que está planejado, em vez de só lembrar que existe algo:

```text
Bom dia.
Planejamento sugerido: 5 atividades · 3h20 estimadas.
[ Revisar plano ]  [ Tudo certo ]
```

`Revisar plano` abre o aplicativo direto na seção Plano do dia. `Tudo certo`
fecha o aviso e grava a confirmação daquela data — ele não volta, nem depois de
reiniciar o computador.

### 2.10 Temas

Menu da bandeja → `Tema` → `Preto` ou `Translúcido`. A escolha persiste.

### 2.11 Iniciar com o Windows

Vem **ativado por padrão** na primeira execução. Para ligar ou desligar: menu da
bandeja → `Iniciar com o Windows`.

Por baixo, é uma entrada por usuário em:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run    valor: HojeWidget
```

Desativar remove apenas essa entrada. Se você mover o aplicativo de pasta, o
caminho é regravado automaticamente na próxima execução.

### 2.12 Onde ficam os dados

```text
%LOCALAPPDATA%\HojeWidget\app.db      banco SQLite (tarefas e configurações)
%LOCALAPPDATA%\HojeWidget\logs\       log técnico local, pequeno e rotativo
```

Nada é gravado ao lado do executável e nada sai da máquina. Para começar do
zero, feche o aplicativo e apague a pasta `%LOCALAPPDATA%\HojeWidget`.

---

## 3. Limitações técnicas reais encontradas

### 3.1 Smart App Control pode bloquear o executável temporariamente

O Smart App Control está **ligado** neste Windows
(`HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy` →
`VerifiedAndReputablePolicyState = 1`). Ele avalia executáveis sem assinatura de
um publicador reconhecido — como este, que é um binário pessoal — e, enquanto o
veredito de reputação não chega, responde:

```text
Uma política de Controle de Aplicativo bloqueou este arquivo.
```

Foi o que aconteceu logo depois da primeira publicação: o `.exe` ficou bloqueado
por vários minutos, dentro e fora do OneDrive, e **depois passou a abrir
normalmente sem nenhuma mudança de configuração**. O veredito é por reputação e
muda com o tempo. Um certificado autoassinado não ajuda: o Smart App Control
confia em raízes próprias, não em uma raiz instalada localmente.

**Hoje o `.exe` abre normalmente e é assim que o aplicativo deve ser usado.**

Se o bloqueio voltar depois de uma nova publicação (o binário muda de hash e a
avaliação recomeça), há duas saídas:

**a) Esperar.** Costuma liberar sozinho em alguns minutos.

**b) Rodar pelo runtime compartilhado do .NET**, sem mexer em segurança nenhuma.
Existe uma segunda publicação, dependente do runtime, em:

```text
%LOCALAPPDATA%\Programs\HojeWidget-dotnet\HojeWidget.dll
```

É para lá que o atalho **`Hoje Widget`** (menu Iniciar e área de trabalho) aponta
hoje, porque é o caminho que funciona sempre.

**A janela de console foi eliminada.** O `dotnet.exe` é um programa de console:
aberto pelo Explorer, o Windows 11 abria junto uma janela do Windows Terminal,
que ficava visível, herdava o nome do atalho e — pior — **encerrava o aplicativo
quando era fechada**. A solução está dentro do próprio aplicativo: logo no início
ele chama `FreeConsole` quando detecta um console associado ao processo. Sem
nenhum processo associado, o Windows fecha o console sozinho. Na publicação
self-contained não há console e a chamada não faz nada.

Para iniciar pela linha de comando, atenção à sintaxe do shell — não são
intercambiáveis:

```powershell
& "$env:ProgramFiles\dotnet\dotnet.exe" "$env:LOCALAPPDATA\Programs\HojeWidget-dotnet\HojeWidget.dll"
```

```bat
"C:\Program Files\dotnet\dotnet.exe" "%LOCALAPPDATA%\Programs\HojeWidget-dotnet\HojeWidget.dll"
```

O primeiro é PowerShell: precisa do operador de chamada `&` porque a linha
começa com uma string entre aspas, e usa `$env:VAR`. O segundo é `cmd.exe`, que
usa `%VAR%`. Rodar a forma de `cmd` no PowerShell dá
`Token ... inesperado na expressão ou instrução`.

O `dotnet.exe` é assinado pela Microsoft e passa pelo Smart App Control. Exige o
**.NET 8 Desktop Runtime** instalado (já está, veio junto com o SDK). A
inicialização automática detecta esse modo sozinha e grava no registro o comando
completo com o `.dll` como argumento.

**c) Desligar o Smart App Control** — a solução definitiva, se você quiser o
`.exe` self-contained que a especificação pede. Configurações → Privacidade e
segurança → Segurança do Windows → Controle de aplicativos e navegador →
Configurações do Controle de Aplicativos Inteligente → Desativado.
⚠️ **Desligar é irreversível:** só dá para religar redefinindo o Windows. É uma
decisão de segurança da máquina, não uma necessidade do aplicativo — com o modo
(b) o widget funciona igual, incluindo a inicialização com o Windows.

Esse segundo modo de publicação **não faz parte da especificação** — foi
acrescentado porque, sem ele, o aplicativo não rodaria nesta máquina.

### 3.2 Estratégia de hospedagem na área de trabalho — e por que não é `SetParent`

A especificação sugere associar o HWND do widget à janela `WorkerW`/`Progman`
com `SetParent`. **Isso não funciona com WPF, e foi verificado na prática.**

O que acontece com `SetParent`: do ponto de vista de janelas, tudo parece certo.
O HWND existe, fica na posição correta, `WindowFromPoint` devolve o widget no
ponto exato, `IsWindowVisible` é `true`, o estilo estendido está correto. Mas o
DWM não compõe o conteúdo de uma janela WPF reparentada para dentro do shell, e
**o widget simplesmente nunca pinta**. A tela mostra só o papel de parede. Foi
confirmado nos dois casos, com `AllowsTransparency` ligado e desligado, movendo o
widget para uma área livre da área de trabalho e fotografando a tela.

O que o aplicativo faz no lugar: a mesma busca pela janela do shell — localizar
quem hospeda o `SHELLDLL_DefView` (o `Progman` ou um `WorkerW`, conforme a versão
do Windows e o modo do papel de parede), enviando antes a mensagem não
documentada `0x052C` ao `Progman` nas duas variantes conhecidas — mas em vez de
reparentar, **ancora o widget por ordem Z**:

- `SetWindowPos(widget, progman, …)` insere o widget imediatamente acima da área
  de trabalho na ordem Z global, ou seja, abaixo de qualquer aplicativo normal;
- a janela intercepta `WM_WINDOWPOSCHANGING` e reescreve `hwndInsertAfter` para
  a âncora, removendo `SWP_NOZORDER`. Assim, nem clicar no widget o traz para
  frente dos aplicativos;
- uma reancoragem a cada 2 segundos cobre o que não avisa a janela: "Mostrar
  área de trabalho" (que eleva o `Progman` acima de nós) e o reinício do
  Explorer (que destrói a âncora anterior, revalidada por `IsValidHost`).

O resultado é o comportamento que a especificação pede — acima do papel de
parede, junto aos ícones, interativo, fora do `Alt + Tab` e da barra de tarefas
(`WS_EX_TOOLWINDOW`), **nunca** `Topmost` nesse modo — com a vantagem de a janela
continuar sendo de nível superior, então a renderização do WPF funciona
normalmente. Nesta máquina a âncora resolvida é o `Progman`.

Posição e tamanho são aplicados em pixels físicos com `SetWindowPos` em duas
passadas: aplica, confere com `GetWindowRect` e corrige pela diferença medida.

### 3.3 Transparência e cantos arredondados

O tema Translúcido **não** usa `AllowsTransparency` do WPF. Esse caminho desenha
por `UpdateLayeredWindow`, que não funciona em janela filha — e foi ele que, na
primeira versão, deixou o widget invisível enquanto ele era filho do `Progman`.
Mesmo depois de trocar a hospedagem por ordem Z, o mecanismo escolhido foi outro,
mais simples e à prova desse tipo de falha:

- a janela é opaca (`AllowsTransparency="False"`) e pinta normalmente;
- a transparência do tema Translúcido é aplicada à janela inteira por
  `SetLayeredWindowAttributes` com alfa 214 (≈84%, dentro da faixa de 80% a 85%
  pedida). Como o alfa vale para a janela toda, o texto também fica levemente
  translúcido; sobre a superfície escura ele continua legível;
- os cantos arredondados vêm de `SetWindowRgn` com `CreateRoundRectRgn`,
  atualizado a cada mudança de tamanho. Os cantos arredondados do DWM não
  serviriam: dependem de efeitos que não se aplicam a esse cenário.

Um efeito colateral de qualquer janela `WS_EX_LAYERED`: `PrintWindow` não a
captura (devolve preto). Isso afeta ferramentas de captura, não o uso.

### 3.4 Nenhuma moldura do sistema

Mesmo com `WindowStyle="None"`, o WPF mantém `WS_THICKFRAME` para permitir
redimensionar pelas bordas. Sem `AllowsTransparency` essa moldura passa a ser
desenhada e aparece como uma faixa clara no topo do widget. Como o widget
redimensiona pelas próprias alças, `WS_CAPTION` e `WS_THICKFRAME` são removidos
do estilo da janela em `SourceInitialized`, com `SWP_FRAMECHANGED`.

### 3.5 Mover e redimensionar são manuais

Como `WS_THICKFRAME` é removido (seção 3.4), não existe borda de
redimensionamento do sistema. Mover pelo cabeçalho e redimensionar pelas alças
invisíveis usam captura de mouse e `SetWindowPos` em pixels físicos, com o mesmo
código nos dois modos.

### 3.6 Decisões menores registradas

- **`+ Adicionar tarefa` no aviso de revisão não fecha o aviso.** A
  especificação diz que ele "abre o campo de criação" e que apenas `Tudo certo`
  "grava a confirmação daquela data". A implementação segue isso ao pé da letra:
  o aviso permanece até `Tudo certo`.
- **Menu `⋯` e confirmação de exclusão são desenhados dentro do widget**, não em
  `Popup`. Um `Popup` do WPF cria uma segunda janela de nível superior, que
  apareceria sobre outros aplicativos e escaparia da hospedagem na área de
  trabalho.
- **Layout salvo relativo ao monitor.** As colunas `x_dip`/`y_dip` guardam o
  deslocamento a partir da origem da área útil do monitor de destino, em DIPs
  daquele monitor. Guardar coordenadas absolutas da área virtual misturaria
  telas com escalas diferentes.
- **O campo de lembrete fica no lugar do título, e não no fim da lista.** Na
  altura padrão, o terço de baixo tem uns 50 px de lista — menos que campo e
  linha juntos. Com o campo no fim da lista, o lembrete recém-criado nunca
  aparecia ao lado dele. No título, a lista fica inteira e rola até o novo.
- **Teste que processa a fila do WPF não pode criar o `App` na própria thread.**
  O construtor de um `Application` agenda o `OnStartup` na fila da thread que o
  cria. Processar essa fila roda a inicialização de verdade: abre o banco real e,
  com o widget aberto, avisa a instância em execução e se desliga — levando junto
  a janela do teste. `ReminderScrollTests` cria o `App` numa thread que nunca
  processa a fila.
- **`sort_order` é uma sequência global.** Concluir uma tarefa não altera o
  `sort_order`; é ele que devolve a tarefa à posição manual anterior quando ela
  é desmarcada. Reordenar redistribui entre as pendentes as mesmas posições que
  elas já ocupavam, dentro de uma transação.
- **`ImplicitUsings` desativado.** O projeto usa WPF e Windows Forms juntos (a
  bandeja usa `NotifyIcon`), e o conjunto padrão de usings implícitos do Windows
  Forms importa `System.Drawing` e `System.Windows.Forms` em todos os arquivos,
  tornando ambíguos nomes como `Application`, `Point`, `TextBox` e
  `MouseEventArgs`. Os usings globais ficam em `src/HojeWidget/GlobalUsings.cs`.
- **`NoWarn=WFAC010`.** O analisador do Windows Forms sugere declarar o DPI por
  propriedade do projeto; a especificação exige `PerMonitorV2` no manifesto, que
  é também o que o WPF lê.

---

## 4. Como desenvolver, testar e publicar

Requer o **.NET 8 SDK**.

```bash
dotnet restore
```

```bash
dotnet build HojeWidget.sln -c Release
```

```bash
dotnet test HojeWidget.sln -c Release --no-build
```

```bash
dotnet publish src/HojeWidget/HojeWidget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Publicação alternativa, dependente do runtime (seção 3.1):

```bash
dotnet publish src/HojeWidget/HojeWidget.csproj -c Release --self-contained false -o "%LOCALAPPDATA%\Programs\HojeWidget-dotnet"
```

### 4.1 Estrutura

```text
HojeWidget.sln
src/HojeWidget/
  App.xaml                     recursos e conversores da aplicação
  GlobalUsings.cs              usings globais explícitos
  app.manifest                 PerMonitorV2, asInvoker
  Assets/app.ico               ícone do aplicativo e da bandeja
  Domain/                      regras puras, sem WPF e sem banco
    TaskTitle / PendingCounter validação de texto e contagem
    LocalDate                  data civil local, semana, nomes em português
    TaskArranger               Hoje, Algum dia, semana e ordem manual
    TaskLabels                 "planejada para ontem · prazo amanhã"
    Deadlines                  próximos prazos e grade do calendário
    RecurrenceCalculator       os quatro tipos de recorrência
    DayEntry / DayBoard        mistura tarefas, rotinas e sessões num quadro do dia
    Progress / Estimate        "3 de 7 concluídas", "1h30", leitura de duração
    Planning/
      Distributor              distribuição automática e margem de segurança
      PriorityEngine           os pesos do motor, todos num arquivo só
      PlanningEngine           plano do dia, carga, sobrecarga e conflitos
    Study/
      StudyGraph               árvore, grafo, ordem topológica e estados
      DependencyValidation     self-dependency, duplicidade e ciclos
      StudyPlanner             o planejador de estudos de uma prova
  Models/                      tarefa, projeto, prova, rotina, prioridade, tema, layout
  Interop/NativeMethods.cs     único arquivo com P/Invoke
  Services/
    Database/                  SQLite, migrações e repositórios
    DesktopHost/               ancoragem por ordem Z na área de trabalho
    DisplayLayout/             assinatura de monitores e posicionamento seguro
    GlobalHotkey/              Ctrl + Shift + Q
    Startup/                   inicialização com o Windows (abstraída)
    System/                    relógio, caminhos, log, instância única, mensagens
    Tray/                      ícone e menu da bandeja
    TaskListService.cs         tarefas, planejamento e prazo
    RoutineService.cs          rotinas, geração e conclusão de ocorrências
    ContainerService.cs        projetos, entregas, subtarefas e distribuição
    ExamService.cs             provas, sessões e progresso objetivo
    StudyGraphService.cs       nós, dependências e integridade do grafo
    ExamPlanningService.cs     junta capacidade geral e limites da prova
    CapacityService.cs         capacidade semanal e exceções por data
    PlanningService.cs         reúne os dados e leva as propostas ao usuário
    DailyReviewService.cs      aviso de revisão diária
    ThemeManager.cs            troca entre os dois temas
    WidgetController.cs        orquestra modos, monitores, dia e ciclo de vida
  ViewModels/                  MVVM simples, sem framework
  Views/
    MainWindow.xaml            o widget
    MainAppWindow.xaml         o aplicativo principal
  Themes/                      Black.xaml, Translucent.xaml, Controls.xaml
tests/HojeWidget.Tests/        316 testes xUnit
```

### 4.2 Esquema do banco

Migrações versionadas em `schema_info`. Versão atual: **7**.

| Versão | O que a migração faz |
| --- | --- |
| 1 | cria `tasks`, `settings`, `display_layouts`, `daily_review` |
| 2 | `tasks.planned_date` + preenche as tarefas existentes para elas não sumirem |
| 3 | `tasks.due_date` |
| 4 | cria `routines` e `routine_occurrences` |
| 5 | cria `containers`; `tasks.parent_id`, `estimated_minutes`, `can_split` |
| 6 | cria `exams`, `exam_topics`, `study_sessions` |
| 7 | prioridade e marca de planejamento manual; `weekly_capacity`, `capacity_overrides`, `daily_plan` |
| 8 | cria `study_nodes` e `study_dependencies`; limites de estudo em `exams`; `study_sessions.node_id` |

Toda migração é **aditiva**: nenhuma versão apaga dados das anteriores, e a
migração roda em transação. Nenhuma tarefa é convertida automaticamente em rotina.

O único ponto que precisou de cuidado foi a V0.2: como a V0.1 não tinha data
planejada e o widget passou a mostrar só o que está planejado até hoje, as
tarefas antigas cairiam em "Algum dia" e **sumiriam da tela**. A migração dá a
elas uma data — a da conclusão, quando houver, ou a data da própria migração —
para que continuem visíveis. Há teste para isso.

`routine_occurrences` tem `UNIQUE (routine_id, occurrence_date)` e
`FOREIGN KEY (routine_id) REFERENCES routines (id) ON DELETE CASCADE`.
A constraint única é o que torna a geração de ocorrências idempotente.

A V0.7 tem o segundo ponto que exigiu cuidado: tudo o que já tinha data
planejada antes dela foi planejado **à mão**, por você. Se a migração deixasse
esses itens sem a marca `manual_planning`, o motor novo poderia propor mudá-los
de dia como se fossem sugestão dele. A migração marca todos, e há teste para
isso. `weekly_capacity` também é semeada com 3h em todos os dias — o mesmo valor
que a V0.5 já usava como padrão —, para que o upgrade não mude comportamento
nenhum.

Duas decisões de modelagem que valem registro, porque as especificações deixavam
espaço para as duas leituras:

- **Subtarefa é uma linha de `tasks` com `parent_id`**, e não uma tabela
  separada. É isso que faz "mover uma tarefa existente para um projeto"
  preservar texto, conclusão, datas, duração e ordem sem copiar nada — e é o que
  garante estruturalmente o único nível de subtarefa, já que o pai é sempre um
  container. `ON DELETE SET NULL` faz a exclusão do projeto devolver as etapas
  em vez de apagá-las.
- **Conteúdos e listas de exercícios moram na mesma tabela**, separados por
  `kind`. A V0.6 §5 lista "Lista 1, Lista 2" entre os conteúdos com nível de
  domínio, e a §6 acrescenta a elas questões e estimativa — são o mesmo objeto
  com campos a mais. Uma tabela só evita que "Lista 1" exista duas vezes, e o
  `kind` é o que mantém os dois contadores de progresso da §11 separados. A
  V0.7.1 levou essa tabela adiante virando `study_nodes`, com tipo, hierarquia e
  conclusão própria.

A migração da V0.7.1 tinha um detalhe que decidiu o desenho: **cada `exam_topic`
virou um `study_node` com o mesmo id**. Isso mantém as sessões já existentes
ligadas ao conteúdo certo sem precisar adivinhar nada — `study_sessions.node_id`
recebe o `topic_id` antigo tal como está.

Duas coisas a migração deliberadamente **não** faz:

- não inventa hierarquia: os conteúdos antigos entram todos na raiz, para você
  agrupá-los em assuntos como quiser;
- não inventa dependência nenhuma. Não há como saber, olhando os dados da V0.6,
  qual conteúdo era pré-requisito de qual — e uma dependência inventada travaria
  um item que você nunca pediu para travar.

A tabela `exam_topics` continua no banco, sem ninguém escrever nela: ela é o
retrato do que existia antes da refatoração.

Todas as consultas são parametrizadas; alterações de ordenação rodam em transação.

---

## 5. Testes

### 5.1 Automatizados — 316 testes, todos passando

```text
Aprovado! – Com falha: 0, Aprovado: 316, Ignorado: 0, Total: 316
```

**V0.1** — validação e normalização de título; criação, edição, conclusão e
desmarcação; agrupamento de pendentes antes de concluídas; persistência da ordem
manual; filtro de concluídas por data local; transição de dia; singular/plural da
contagem; estado vazio versus tudo concluído; aviso diário uma vez por data;
geração determinística da assinatura de monitores; seleção do layout correto por
assinatura; ajuste de posição e tamanho para dentro da área útil; persistência
SQLite em banco temporário; abstração da inicialização sem tocar no registro real.

**V0.2** — tarefa de hoje, futura, atrasada e sem data planejada; separação entre
Hoje, Semana e Algum dia; arrastar entre dias alterando `PlannedDate` e
persistindo; tarefa que volta ao widget ao ser movida para hoje; conclusão
seguindo a regra anterior; migração V0.1 → V0.2 preservando tarefas,
configurações e layouts, e mantendo as tarefas antigas visíveis.

**V0.3** — prazo futuro, hoje e vencido; independência entre `PlannedDate` e
`DueDate` nas quatro combinações; criar, alterar e remover prazo; ordenação de
próximos prazos; exclusão de concluídas e de tarefas sem prazo; posicionamento
correto de planejados e prazos no calendário; grade do mês começando na segunda;
rótulos combinados; migração V0.2 → V0.3.

**V0.4** — os quatro tipos de recorrência; alinhamento de "a cada X dias" quando
a janela começa depois do início; fevereiro com dia 30 e 31; ano bissexto;
idempotência da geração (rodar três vezes não duplica); regra incompleta não cria
rotina; conclusão que não encerra a rotina; independência entre ocorrências;
ocorrência de ontem que sai de Hoje; edição que mantém o passado e recalcula o
futuro; exclusão que remove as ocorrências; mistura de tarefas e ocorrências em
Hoje; migração V0.3 → V0.4.

**V0.5** — projeto e entrega como conceitos separados; um nível só de subtarefa;
progresso `3 de 7` e a barra em texto; a última etapa que pergunta em vez de
concluir; mover tarefa existente preservando texto, conclusão, datas, duração e
ordem; exclusão que devolve as etapas em vez de apagá-las; subtarefa que não
polui "Algum dia" mas aparece em Hoje com o progresso do projeto; distribuição
que espalha e guarda a véspera; distribuição que encosta no prazo sem amontoar;
dia de capacidade 0 ignorado; item divisível virando poucas sessões; item indivisível
achando um dia inteiro, e informando conflito quando não há; prazo apertado que
gasta a margem e avisa; duração desconhecida que não vira número inventado;
plano aplicado criando as sessões como etapas irmãs; redistribuir sem empilhar
sufixos; leitura de `1h30`, `90`, `1:30`; migração V0.4 → V0.5.

**V0.6** — criar e editar prova; domínio nos quatro níveis; lista com questões e
estimativa; os dois contadores de progresso separados; plano de estudo deixando a
véspera para revisão; plano aplicado gerando sessões que aparecem no dia; a linha
`1h30 · prova em 4 dias`; conteúdo já dominado saindo da fila; prova amanhã que
usa o que sobra e avisa; remoção de conteúdo levando só as sessões pendentes;
exclusão da prova em cascata; calendário distinguindo prova, sessão e prazo;
replanejar substituindo só as sessões pendentes; migração V0.5 → V0.6.

**V0.7** — prazo amanhã contra prazo distante; vencido na frente; prioridade
alta e baixa desempatando; atraso no planejamento; prova próxima com domínio
fraco; não estudado pesando mais que razoável; explicação em texto e não em
número; ordem determinística; sobrecarga detectada; dia de descanso que não é
sobrecarga; redistribuição que tira o excesso sem usar dia zerado; redistribuição
que não mexe no planejado à mão; margem preservada; rotina que não é movida;
conflito informado com os números; horizonte curto; atrasado entrando no plano de
hoje; capacidade semanal, exceção por data e persistência das duas; plano do dia
misturando tarefa, rotina e sessão; ordem manual preservada; replanejamento que
propõe e só grava depois do aceite; terminar tudo sem gerar trabalho extra;
migração V0.6 → V0.7 marcando como manual o que já tinha dia.

**V0.7.1** — árvore com assuntos e filhos; assunto sem domínio nem duração;
dependência simples, múltipla e transitiva; só a conclusão real destrava;
data planejada, sessão criada e sessão parcial **não** destravam; nó bloqueado
fora dos candidatos e sem consumir capacidade; self-dependency, duplicidade e
ciclo recusados com mensagem; dependência automática teoria → lista, recusável na
criação e removível depois; desbloqueio que não joga o item no dia de hoje;
mover na árvore sem mexer no grafo; máximo diário e máximo por sessão nunca
ultrapassados; meta confortável respeitada com prova distante e elevada com prova
próxima, sempre dentro do máximo; capacidade geral já tomada limitando a prova;
24h de capacidade geral que não viram 24h da matéria; divisível fatiado em poucas
sessões; indivisível inteiro, e conflito quando não cabe numa sessão; véspera de
revisão preservada e cedida só quando precisa; plano impossível com o déficit em
números; conclusão parcial; apertar limite marcando sessões para revisão;
migração V0.7 → V0.7.1 num banco criado de verdade na versão antiga.

**Ordem e pendências** — a Lista 3 planejada, mas nunca antes da Lista 2; com
pouco tempo, a vez é de quem vem antes; profundidade da cadeia dando a ordem
topológica; quem espera a vez também consome capacidade e também chega ao widget;
mudar dependência sem invalidar sessão já marcada; tarefa de ontem e de semanas
atrás vindo para hoje; concluída, futura e sem data ficando como estão;
ocorrência de rotina que não é arrastada; sessão de estudo atrasada que vem
junto; marca de escolha manual preservada; contagem do aviso do dia que só vale
para hoje; rodar duas vezes sem mudar nada na segunda.

**Atalho para o Claude** — a opção fecha o menu e só pede para abrir, sem criar
nada no widget; o endereço é `https` em `claude.ai/new` e não carrega parâmetro
nenhum; o teste de desenho confere que a opção aparece inteira no menu. Abrir o
navegador de verdade não é testado: seria abrir uma aba em quem roda os testes.

**Lembretes** — lembrete nascendo sem data e fora de Hoje; campo que fica aberto
para o próximo; texto vazio que não cria nada; riscar lembrete sem virar "Tudo
concluído por hoje"; concluído saindo na virada do dia; pendente que não é
trazido para hoje; as mesmas tarefas de Algum dia; etapa de projeto que não vira
lembrete; o rodapé com as duas contagens; abrir um campo fechando o outro;
editar e excluir; editar numa lista fechando a edição da outra; e, numa janela
mostrada de verdade, o lembrete recém-criado visível depois do `Enter` mesmo com
a lista cheia, e nenhuma barra de rolagem sobrando na lista vazia. O teste de
desenho mede que os lembretes começam depois de dois terços da altura.

**Data-limite para começar (entregas)** — a entrega distante que não começa hoje;
as etapas encostando no prazo sem inverter a ordem; um dia depois do limite já
não cabendo; sessões escolhidas de trás para frente mas numeradas na ordem do
calendário; dia fechado recuando o início; item indivisível pegando o dia mais
tarde que o comporta; duração desconhecida também encostando; plano vazio que não
recomenda dia nenhum; e a tela anunciando a data-limite com as datas em leitura.

**Data-limite para começar (provas)** — a prova MTM-25 real (três aulas e uma
lista que depende delas, prova daqui a três semanas) começando em 09/11 e
terminando encostada na margem, e não em 31/10; começar um dia depois do limite
realmente passando da meta confortável; um item indivisível impossível que vira
conflito sem puxar o plano para hoje; prova distante que não começa hoje; a conta de
dois dias de conteúdo no ritmo confortável, com a margem no lugar certo; um dia
depois do limite já estourando a meta; prova próxima que começa hoje mesmo; dia
de descanso que não entra na conta; o que já está tomado no dia encurtando a
janela; a recomendação vindo como resultado e não como recado no fim; e a ordem
das dependências continuando a valer dentro da janela recuada.

O exemplo inteiro da especificação (a MPP-31 com Torção, Flexão Bi-Axial e
Paredes Finas) é montado pelos serviços reais num teste de integração, que
verifica os dois casos que a especificação exige em letras maiúsculas: a Lista 3
que nunca vem antes da Lista 2, e as 24h de capacidade geral que não viram 24h
de estudo da mesma prova.

Dois testes cuidam do que o WPF quebra em silêncio:

- um **desenha as janelas fora da tela** e confere que elas produzem pixels — o
  widget nos dois temas (superfície sólida, texto claro) e o aplicativo principal
  em **cada uma das onze seções**. Cada execução deixa os PNGs em
  `%TEMP%\HojeWidgetRender\`;
- outro liga o **rastreamento de binding do WPF** e falha se qualquer seção
  produzir erro. Ele nasceu de um bug real: `Visibility` e `DataContext` no mesmo
  elemento fazem a `Visibility` ser resolvida contra o novo contexto, o binding
  falha calado, a seção nunca some e três telas ficam desenhadas uma sobre a
  outra. Nada estoura; só fica errado. Agora estoura.

### 5.2 Verificado manualmente nesta máquina

Ambiente: Windows 11 Home Single Language, dois monitores 1920×1080, o
secundário a 125%.

- [x] **O widget aparece de verdade na área de trabalho.** Confirmado por
      fotografia da tela: com o widget movido para uma área livre do papel de
      parede, ele é desenhado por completo — superfície escura, cantos
      arredondados, cabeçalho `HOJE`, aviso de revisão, `Nada por enquanto.` e o
      contador. Esta é a verificação que faltava nas primeiras rodadas.
- [x] Fica atrás dos aplicativos normais: no ponto onde o widget está,
      `WindowFromPoint` devolve a janela do aplicativo em primeiro plano, não o
      widget.
- [x] Ancoragem por ordem Z: janela de nível superior (`pai = #32769`),
      `TOPMOST=False`, `WS_EX_TOOLWINDOW` ativo, `WS_EX_APPWINDOW` desligado.
- [x] Fora da barra de tarefas e do `Alt + Tab` (`WS_EX_TOOLWINDOW`).
- [x] `Ctrl + Shift + Q` alterna os modos. Medido em três alternâncias seguidas,
      com posição e tamanho preservados exatamente (`1486,24 410x300` em todas).
- [x] Atalho global registrado na inicialização (registrado no log).
- [x] Instância única: a segunda execução sinaliza a primeira e encerra;
      permanece 1 processo, e a primeira registra "Segunda execucao detectada".
- [x] Posição inicial de fábrica: canto superior direito do monitor principal
      com margem de 24 (`1920 − 410 − 24 = 1486`, `y = 24`).
- [x] Banco e log criados em `%LOCALAPPDATA%\HojeWidget`.
- [x] Entrada `Run` criada na primeira execução, com caminho absoluto e entre
      aspas; corrigida sozinha quando o executável muda de lugar ou de modo.
- [x] Nenhuma moldura do sistema visível (a faixa clara no topo, causada por
      `WS_THICKFRAME`, foi eliminada).
- [x] Nenhuma janela de console e nenhuma janela na barra de tarefas ao abrir
      pelo `.exe`.

### 5.3 Ainda não verificado — depende de interação ou de hardware

Estes itens não puderam ser exercitados sem operar o mouse na máquina em uso ou
sem mudar o hardware. Vale conferir no primeiro dia de uso:

- [ ] Menu da bandeja: `Abrir aplicativo`, `Mostrar`/`Ocultar`, troca de tema,
      `Iniciar com o Windows`, `Sair` encerrando o processo de fato.
- [ ] Arrastar tarefas com o mouse para reordenar e o marcador de inserção.
- [ ] Mover e redimensionar o widget com o mouse.
- [ ] **Arrastar uma tarefa de um dia para outro** na seção Esta semana.
- [ ] Criar tarefa pelo aplicativo com cada opção de "Quando pretende fazer?",
      com prazo, duração, divisibilidade e prioridade.
- [ ] Criar rotina de cada um dos quatro tipos e ver a ocorrência aparecer no
      widget, na semana e no calendário.
- [ ] Editar e excluir rotina pela seção Rotinas.
- [ ] Criar projeto e entrega, adicionar etapas e ver a barra de progresso andar.
- [ ] Marcar a última etapa e responder `Sim, concluir` / `Ainda não`.
- [ ] `⋯ → Mover para…` levando uma tarefa existente para um projeto.
- [ ] `Distribuir automaticamente` com prazo definido: conferir a data-limite e
      que o trabalho encosta no prazo, e o botão `Ajustar` mudando uma data antes
      de aceitar.
- [ ] Criar prova com os três limites e ver o erro quando eles se contradizem.
- [ ] Montar a árvore: `+ Assunto`, depois `+ Teoria` e `+ Lista` dentro dele.
- [ ] Ver a lista nascer bloqueada pela teoria, e desmarcar a caixa antes de criar.
- [ ] Marcar a teoria como concluída e ver a lista deixar de aparecer como "depois de".
- [ ] Deixar algo pendente de ontem e conferir que ele veio para hoje sozinho.
- [ ] Criar lembretes pelo `+` de `LEMBRETES` e vê-los em Algum dia, no app.
- [ ] `+ → Pedir ideias ao Claude` abrindo uma conversa nova no navegador.
- [ ] Redimensionar o widget e ver a divisão dois terços / um terço acompanhar.
- [ ] Tentar criar um ciclo de dependências e ler a recusa.
- [ ] `Gerar plano de estudo` numa prova distante e conferir a data-limite.
- [ ] `Ajustar datas` liberando os campos, e `Aceitar` gravando o plano.
- [ ] Ver a prova marcada no Calendário, no dia dela e em Próximos prazos.
- [ ] `Gerar plano de estudo` e ver as sessões aparecerem em Hoje e na Semana.
- [ ] Reduzir o máximo diário com um plano já feito e ver o aviso de revisão.
- [ ] Alterar a capacidade da semana, zerar um dia e criar uma exceção por data.
- [ ] `Redistribuir` num dia sobrecarregado e conferir a proposta antes de
      aplicar.
- [ ] `Revisar plano` no aviso do widget abrindo direto no Plano do dia.
- [ ] Fechar a janela do aplicativo principal e reabri-la pela bandeja.
- [ ] Reinício do Explorer (`taskkill /f /im explorer.exe` e reabrir):
      o widget deve reancorar e o ícone da bandeja reaparecer.
- [ ] "Mostrar área de trabalho" (`Win + D`): o widget deve continuar visível,
      reancorando em até 2 segundos.
- [ ] Clicar no widget não deve trazê-lo para a frente dos aplicativos.
- [ ] Conectar e desconectar o monitor externo: cada configuração deve
      restaurar seu próprio layout.
- [ ] Dois monitores com escalas de DPI diferentes.
- [ ] Inicialização automática após reiniciar o Windows.
- [ ] Virada de dia real à meia-noite (a lógica está coberta por testes, com o
      relógio controlado).

---

## 6. Fora do escopo

Cada versão congelou o próprio escopo, e nada além dele foi implementado:

- **categorias**, **etiquetas**;
- **sub-subtarefas**, árvore infinita, **Gantt**;
- **dependências entre tarefas comuns** — o grafo da V0.7.1 é só do módulo de
  provas; tarefas, projetos e entregas continuam sem pré-requisitos;
- editor gráfico de nós e linhas soltas: a árvore basta;
- **time blocking** por hora, agenda com horário — aqui a unidade é sempre o dia;
- **frequência flexível** ("Academia 4x por semana e o app escolhe os dias") —
  explicitamente adiada pela V0.4; aqui as regras são sempre explícitas;
- **repetição espaçada** avançada, previsão de nota, score de preparação;
- **notificações**, lembretes, alarmes, sons, Pomodoro;
- **streak**, estatísticas, métricas, gamificação;
- **histórico visível**, lixeira, arquivo, restauração de excluídas;
- **IA**, machine learning, previsão comportamental, sugestões automáticas;
- **integrações** (Google Calendar, Outlook), servidor, API, sincronização,
  conta, login, nuvem, telemetria;
- tela de configurações, atalhos configuráveis, mais de dois temas, modo
  compacto, atualização automática, instalador.

Prioridade, duração estimada, projetos, subtarefas de um nível e planejamento
automático **saíram** desta lista nas versões V0.5 a V0.7, que os pediram
explicitamente. O que continua valendo é o princípio: o algoritmo sugere,
explica e distribui, mas nunca replaneja em silêncio nem substitui o julgamento
do usuário.

Também não há estruturas de interface vazias, botões desabilitados nem modelos
de dados preparados "para depois".
