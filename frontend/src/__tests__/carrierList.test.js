import { initCarrierList, renderCarriers, clearCarrierList } from '../carrierList.js';

describe('carrierList.js', () => {
  let container;

  beforeEach(() => {
    container = document.createElement('div');
    initCarrierList(container);
  });

  test('renders all carriers in input order without re-sorting', () => {
    const carriers = [
      { name: 'Knight-Swift', trucksPerDay: 42 },
      { name: 'J.B. Hunt', trucksPerDay: 30 },
      { name: 'YRC Worldwide', trucksPerDay: 12 },
    ];

    renderCarriers(carriers);

    const items = container.querySelectorAll('.carrier-list-item');
    expect(items).toHaveLength(3);
    expect(items[0].textContent).toContain('Knight-Swift');
    expect(items[0].textContent).toContain('42');
    expect(items[1].textContent).toContain('J.B. Hunt');
    expect(items[2].textContent).toContain('YRC Worldwide');
    expect(container.querySelector('.carrier-list-empty')).toBeNull();
  });

  test('renders a distinct empty state for an empty array, not a blank list or an error', () => {
    renderCarriers([]);

    expect(container.querySelector('.carrier-list')).toBeNull();
    const empty = container.querySelector('.carrier-list-empty');
    expect(empty).not.toBeNull();
    expect(empty.textContent.length).toBeGreaterThan(0);
  });

  test('re-rendering clears the previous result', () => {
    renderCarriers([{ name: 'UPS Inc.', trucksPerDay: 5 }]);
    renderCarriers([]);

    expect(container.querySelectorAll('.carrier-list-item')).toHaveLength(0);
    expect(container.querySelector('.carrier-list-empty')).not.toBeNull();
  });

  test('clearCarrierList removes prior results without showing the empty state', () => {
    renderCarriers([{ name: 'UPS Inc.', trucksPerDay: 5 }]);

    clearCarrierList();

    expect(container.querySelectorAll('.carrier-list-item')).toHaveLength(0);
    expect(container.querySelector('.carrier-list-empty')).toBeNull();
    expect(container.textContent).toBe('');
  });
});
