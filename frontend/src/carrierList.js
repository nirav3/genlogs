let listContainer = null;

export function initCarrierList(container) {
  listContainer = container;
}

export function clearCarrierList() {
  if (!listContainer) {
    throw new Error('carrierList.initCarrierList must be called before clearCarrierList');
  }
  listContainer.textContent = '';
}

export function renderCarriers(carriers) {
  if (!listContainer) {
    throw new Error('carrierList.initCarrierList must be called before renderCarriers');
  }

  listContainer.textContent = '';

  if (!carriers || carriers.length === 0) {
    const empty = document.createElement('p');
    empty.className = 'carrier-list-empty';
    empty.textContent = 'No carriers found for this lane.';
    listContainer.appendChild(empty);
    return;
  }

  const list = document.createElement('ul');
  list.className = 'carrier-list';

  for (const carrier of carriers) {
    const item = document.createElement('li');
    item.className = 'carrier-list-item';

    const name = document.createElement('span');
    name.className = 'carrier-name';
    name.textContent = carrier.name;

    const trucksPerDay = document.createElement('span');
    trucksPerDay.className = 'carrier-trucks-per-day';
    trucksPerDay.textContent = `${carrier.trucksPerDay} trucks/day`;

    item.appendChild(name);
    item.appendChild(trucksPerDay);
    list.appendChild(item);
  }

  listContainer.appendChild(list);
}
