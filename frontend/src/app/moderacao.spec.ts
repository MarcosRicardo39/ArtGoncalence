import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';

describe('Painel de moderação', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    document.cookie = 'XSRF-TOKEN=;max-age=0;path=/';
    http.verify();
  });

  it('mantém a fila e as ações fora da tela quando a API retorna 403', async () => {
    const harness = await RouterTestingHarness.create();
    const navegacao = harness.navigateByUrl('/moderacao');
    const fila = await vi.waitFor(() =>
      http.expectOne('/api/moderacao/pendentes?page=1&pageSize=10'),
    );
    fila.flush({}, { status: 403, statusText: 'Forbidden' });
    await navegacao;
    harness.detectChanges();

    const tela = harness.routeNativeElement!;
    expect(tela.querySelector('[role="alert"]')?.textContent).toContain(
      'exclusiva para moderadores',
    );
    expect(tela.querySelector('.abas')).toBeNull();
    expect(tela.querySelector('article')).toBeNull();
  });

  it('exige justificativa e registra a decisão com CSRF antes de recarregar a fila', async () => {
    document.cookie = 'XSRF-TOKEN=teste;path=/';
    const harness = await RouterTestingHarness.create();
    const navegacao = harness.navigateByUrl('/moderacao');
    const urlFila = '/api/moderacao/pendentes?page=1&pageSize=10';
    const fila = await vi.waitFor(() => http.expectOne(urlFila));
    fila.flush({
      page: 1,
      pageSize: 10,
      total: 1,
      itens: [
        {
          id: 'obra',
          titulo: 'Ilustração de teste',
          descricao: 'Descrição',
          categoria: 'ORIGINAL',
          imagemUrl: '/api/moderacao/obra/imagem',
        },
      ],
    });
    await navegacao;
    harness.detectChanges();
    const tela = harness.routeNativeElement!;
    const acao = Array.from(tela.querySelectorAll('button')).find(
      (botao) => botao.textContent?.trim() === 'Aprovar',
    );
    acao!.click();
    harness.detectChanges();

    const formulario = tela.querySelector('form')!;
    formulario.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    harness.detectChanges();
    expect(tela.querySelector('#justificativa-erro')?.textContent).toContain(
      'Informe a justificativa.',
    );
    http.expectNone('/api/contas/csrf');

    const campo = tela.querySelector('textarea')!;
    campo.value = '  Revisei a obra.  ';
    campo.dispatchEvent(new Event('input', { bubbles: true }));
    formulario.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const csrf = await vi.waitFor(() => http.expectOne('/api/contas/csrf'));
    csrf.flush(null);
    const decisao = await vi.waitFor(() => http.expectOne('/api/moderacao/obra/decisao'));
    expect(decisao.request.body).toEqual({ acao: 'APROVAR', justificativa: 'Revisei a obra.' });
    expect(decisao.request.headers.get('X-XSRF-TOKEN')).toBe('teste');
    decisao.flush(null);
    const atualizada = await vi.waitFor(() => http.expectOne(urlFila));
    atualizada.flush({ page: 1, pageSize: 10, total: 0, itens: [] });
    await vi.waitFor(() => {
      harness.detectChanges();
      expect(tela.textContent).toContain('Decisão registrada.');
      expect(tela.querySelector('article')).toBeNull();
    });
  });

  it('envia a justificativa de encerramento à rota privada de denúncias', async () => {
    document.cookie = 'XSRF-TOKEN=teste;path=/';
    const harness = await RouterTestingHarness.create();
    const navegacao = harness.navigateByUrl('/moderacao');
    const pendentes = await vi.waitFor(() =>
      http.expectOne('/api/moderacao/pendentes?page=1&pageSize=10'),
    );
    pendentes.flush({ page: 1, pageSize: 10, total: 0, itens: [] });
    await navegacao;
    harness.detectChanges();
    const tela = harness.routeNativeElement!;
    const denunciar = Array.from(tela.querySelectorAll('button')).find(
      (botao) => botao.textContent?.trim() === 'Denúncias',
    );
    denunciar!.click();
    const urlFila = '/api/moderacao/denuncias?page=1&pageSize=10';
    http.expectOne(urlFila).flush({
      page: 1,
      pageSize: 10,
      total: 1,
      itens: [
        {
          id: 'denuncia',
          ilustracaoId: 'obra',
          motivo: 'DIREITOS_AUTORAIS',
          descricao: 'Descrição fictícia',
          contato: 'teste@example.test',
          criadoEm: '2026-11-23T12:00:00Z',
        },
      ],
    });
    await vi.waitFor(() => {
      harness.detectChanges();
      expect(tela.textContent).toContain('teste@example.test');
    });
    const encerrar = Array.from(tela.querySelectorAll('button')).find(
      (botao) => botao.textContent?.trim() === 'Encerrar denúncia',
    );
    encerrar!.click();
    harness.detectChanges();

    const campo = tela.querySelector('textarea')!;
    campo.value = 'Revisão concluída.';
    campo.dispatchEvent(new Event('input', { bubbles: true }));
    tela
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    http.expectOne('/api/contas/csrf').flush(null);
    const fechamento = await vi.waitFor(() =>
      http.expectOne('/api/moderacao/denuncias/denuncia/encerrar'),
    );
    expect(fechamento.request.body).toEqual({ justificativa: 'Revisão concluída.' });
    fechamento.flush(null);
    const atualizada = await vi.waitFor(() => http.expectOne(urlFila));
    atualizada.flush({ page: 1, pageSize: 10, total: 0, itens: [] });
    await vi.waitFor(() => {
      harness.detectChanges();
      expect(tela.textContent).toContain('Denúncia encerrada.');
      expect(tela.textContent).not.toContain('teste@example.test');
    });
  });
});
