import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';

describe('Apoio no perfil', () => {
  it('abre o provedor em nova aba sem referência ao perfil', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    const navegacao = harness.navigateByUrl('/artistas/artista');
    const consulta = await vi.waitFor(() => http.expectOne('/api/artistas/artista'));
    consulta.flush({
      id: 'artista',
      nomePublico: 'Artista',
      bio: null,
      apoio: { provedor: 'Ko-fi', url: 'https://ko-fi.com/artista' },
    });
    await navegacao;
    harness.detectChanges();
    const botao = harness.routeNativeElement!.querySelector('a[target="_blank"]');
    expect(botao?.textContent?.trim()).toBe('Apoiar via Ko-fi');
    expect(botao?.getAttribute('href')).toBe('https://ko-fi.com/artista');
    expect(botao?.getAttribute('rel')).toBe('noopener noreferrer');
    http.verify();
  });
});
