import expect from 'unittest/helpers/expect.js';
import sinon from 'sinon';
import React from 'react';
import TestUtils from 'react-addons-test-utils';
import ToolbarFrame from 'console/components/presentation/ToolbarFrame.js';
import Toolbar from 'console/components/presentation/Toolbar.js';
import FormTab from 'console/components/presentation/FormTab.js';

describe('ToolbarFrame', () => {
	let renderer, props, pageActions;
	beforeEach(() => {
		pageActions = {
			fireAction: () => {},
			update: () => {},
			save: () => {}
		};
		props = {
			pageDef: {
				name: 'test',
				tabs: ['test/oneTab'],
				buttons: [
					'test/save',
					'test/onebutton',
					'test/twobutton'
				]
			},
			tabDefs: {
				'test/oneTab': {
					name: 'test/oneTab',
					fieldsets: [
						'test/oneset',
						'test/twoset',
						'test/fourset'
					]
				}
			},
			buttonDefs: {
				'test/onebutton': { label: 'One', action: 'oneaction' },
				'test/twobutton': { label: 'Two', action: 'twoaction' },
				'test/save': { label: 'Save', action: 'save' },
				'do-not-render-button': { label: 'Must not be shown' }
			},
			fieldsetDefs: {
				'test/oneset': {
					label: 'First set',
					fields: [ 'test/oneset/onefield', 'test/oneset/twofield' ]
				},
				'test/twoset': {
					label: 'Second set',
					fields: [ 'test/twoset/threefield' ]
				},
				'no-show-set': {
					label: 'Don\'t show me',
					fields: []
				}
			},
			dataFieldDefs: {
				'test/oneset/onefield': {},
				'test/oneset/twofield': { defaultValue: 'a default' },
				'test/twoset/threefield': { defaultValue: 'overwritten' }
			},
			tabName: 'test/oneTab',
			dirtyPages: {},
			values: {
				'test/twoset/threefield': 'different'
			},
			actions: {
				save: sinon.spy(() => pageActions.save).named('save'),
				fireAction: sinon.spy(() => pageActions.fireAction).named('fireAction'),
				updateValue: sinon.spy(() => pageActions.update).named('threebutton')
			},
		};
		renderer = TestUtils.createRenderer();
	});

	it('renders a toolbar and a single contained tab', () => {
		renderer.render(<ToolbarFrame name='test' {...props}/>);
		return expect(
			renderer,
			'to have rendered',
			<div className='page'>
				<Toolbar
					canSave={false}
					type='document'
					buttons={{}}/>
				<FormTab name='test/oneTab' actions={{}} values={{}} fieldsetDefs={{}} dataFieldDefs={{}} tabDef={{}}/>
			</div>
		)
		.and(
			'queried for', <Toolbar type='document' canSave={false} buttons={{}}/>,
			'to have props exhaustively satisfying', {
				type: 'document',
				canSave: false,
				buttons: {
					'test/save': { label: 'Save', action: pageActions.save, saveButton: true },
					'test/onebutton': { label: 'One', action: pageActions.fireAction },
					'test/twobutton': { label: 'Two', action: pageActions.fireAction }
				}
			}
		);
	});

	it('passes named actions to the toolbar', () => {
		renderer.render(<ToolbarFrame name='test' {...props}/>);
		Promise.all([
			expect(renderer,
				'queried for', <Toolbar type='document' canSave={false} buttons={{}}/>,
			'to have props satisfying', {
				type: 'document',
				buttons: {
					'test/save': { action: expect.it('to be', pageActions.save) },
					'test/onebutton': { action: expect.it('to be', pageActions.fireAction) },
					'test/twobutton': { action: expect.it('to be', pageActions.fireAction) }
				}
			}),
			expect(props.actions.save, 'to have a call satisfying', { args: ['test'] }),
			expect(props.actions.fireAction, 'to have calls exhaustively satisfying', [
				{ args: ['oneaction', 'test'] },
				{ args: ['twoaction', 'test'] },
			])
		]);
	});

	describe('when missing definitions', () => {
		describe('for page', () => {
			beforeEach(() => {
				delete props.pageDef.tabs;
				renderer.render(<ToolbarFrame name='test' {...props}/>);
			});

			it('renders nothing', () => expect(
				renderer.getRenderOutput(),
				'to equal', null
			));
		});

		describe('for tab name', () => {
			beforeEach(() => {
				props.tabName = '';
				renderer.render(<ToolbarFrame name='test' {...props}/>);
			});

			it('renders nothing', () => expect(
				renderer.getRenderOutput(),
				'to equal', null
			));
		});
	});
});