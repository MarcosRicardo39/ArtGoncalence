import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { GaleriaApi } from './galeria-api';

describe('Galeria e publicação', () => {
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

  it('exibe obras e links de página retornados pela API pública', async () => {
    const harness = await RouterTestingHarness.create();
    const navegacao = harness.navigateByUrl('/');
    const requisicao = await vi.waitFor(() =>
      http.expectOne('/api/ilustracoes?page=1&pageSize=12'),
    );
    requisicao.flush({
      page: 1,
      pageSize: 12,
      total: 13,
      itens: [
        {
          id: 'obra',
          titulo: 'A baía',
          categoria: 'ORIGINAL',
          artista: { id: 'artista', nomePublico: 'Artista' },
          imagemUrl: '/api/ilustracoes/obra/imagem',
        },
      ],
    });
    await navegacao;
    harness.detectChanges();
    const tela = harness.routeNativeElement!;
    expect(tela.querySelector('article h3')?.textContent).toBe('A baía');
    expect(tela.querySelector('article img')?.getAttribute('alt')).toBe('A baía');
    expect(tela.querySelector('.galeria-paginacao a')?.getAttribute('href')).toBe('/?page=2');
  });

  it('obtém CSRF antes de enviar o multipart de publicação', async () => {
    document.cookie = 'XSRF-TOKEN=token-de-teste;path=/';
    const formulario = new FormData();
    formulario.append('titulo', 'A baía');
    const envio = TestBed.inject(GaleriaApi).publicar(formulario);
    http.expectOne('/api/contas/csrf').flush(null);
    await Promise.resolve();
    const requisicao = http.expectOne('/api/ilustracoes');
    expect(requisicao.request.method).toBe('POST');
    expect(requisicao.request.headers.get('X-XSRF-TOKEN')).toBe('token-de-teste');
    expect(requisicao.request.headers.has('Content-Type')).toBe(false);
    expect(requisicao.request.body).toBe(formulario);
    requisicao.flush({ id: 'obra', status: 'PENDENTE' });
    await expect(envio).resolves.toEqual({ id: 'obra', status: 'PENDENTE' });
  });

  it('exige contato em denúncia de direitos autorais e envia pelo canal público com CSRF', async () => {
    document.cookie = 'XSRF-TOKEN=token-de-teste;path=/';
    const harness = await RouterTestingHarness.create();
    const navegacao = harness.navigateByUrl('/ilustracoes/obra');
    const consulta = await vi.waitFor(() => http.expectOne('/api/ilustracoes/obra'));
    consulta.flush({
      id: 'obra',
      titulo: 'A baía',
      categoria: 'ORIGINAL',
      artista: { id: 'artista', nomePublico: 'Artista' },
      imagemUrl: '/api/ilustracoes/obra/imagem',
    });
    await navegacao;
    harness.detectChanges();
    const tela = harness.routeNativeElement!;
    tela.querySelector<HTMLButtonElement>('button[type=button]')!.click();
    harness.detectChanges();
    await harness.fixture.whenStable();
    const motivo = tela.querySelector<HTMLSelectElement>('#motivo-denuncia')!;
    const descricao = tela.querySelector<HTMLTextAreaElement>('#descricao-denuncia')!;
    const contato = tela.querySelector<HTMLInputElement>('#contato-denuncia')!;
    const botao = tela.querySelector<HTMLButtonElement>('button[type=submit]')!;
    motivo.value = 'DIREITOS_AUTORAIS';
    motivo.dispatchEvent(new Event('change'));
    descricao.value = 'Descrição da denúncia';
    descricao.dispatchEvent(new Event('input'));
    harness.detectChanges();
    expect(botao.disabled).toBe(true);
    contato.value = 'titular@example.test';
    contato.dispatchEvent(new Event('input'));
    await vi.waitFor(() => {
      harness.detectChanges();
      expect(botao.disabled).toBe(false);
    });
    botao.click();
    http.expectOne('/api/contas/csrf').flush(null);
    const requisicao = await vi.waitFor(() => http.expectOne('/api/ilustracoes/obra/denuncias'));
    expect(requisicao.request.method).toBe('POST');
    expect(requisicao.request.headers.get('X-XSRF-TOKEN')).toBe('token-de-teste');
    expect(requisicao.request.body).toEqual({
      motivo: 'DIREITOS_AUTORAIS',
      descricao: 'Descrição da denúncia',
      contato: 'titular@example.test',
    });
    requisicao.flush(null, { status: 202, statusText: 'Accepted' });
    await vi.waitFor(() => {
      harness.detectChanges();
      expect(harness.routeNativeElement!.textContent).toContain(
        'Denúncia recebida pela moderação.',
      );
    });
  });
});
