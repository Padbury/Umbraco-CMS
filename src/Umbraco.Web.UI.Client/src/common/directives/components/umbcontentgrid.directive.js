/**
@ngdoc directive
@name umbraco.directives.directive:umbContentGrid
@restrict E
@scope

@description
Use this directive to generate a list of content items presented as a flexbox grid.

<h3>Markup example</h3>
<pre>
    <div ng-controller="Umbraco.Controller as vm">

        <umb-content-grid
           content="vm.contentItems"
           content-properties="vm.includeProperties"
           on-click="vm.clickItem"
           on-select="vm.selectItem">
        </umb-content-grid>

    </div>
</pre>

<h3>Controller example</h3>
<pre>
    (function () {
        "use strict";

        function Controller(myService) {

            var vm = this;
            vm.contentItems = [];
            vm.includeProperties = {
            {
                  "alias": "sortOrder",
                  "header": "Sort"
                },
                {
                  "alias": "updateDate",
                  "header": "Last edited"
                },
                {
                  "alias": "owner",
                  "header": "Created by"
                }
            };

            vm.clickItem = clickItem;
            vm.selectItem = selectItem;

            myService.getContentItems().then(function(contentItems){
                vm.contentItems = contentItems;
            });

            function clickItem(item){
                // do magic here
            }

            function selectItem(item, event, index) {
                // set item.selected = true; to select the item
                // do magic here
            }

        }

        angular.module("umbraco").controller("Umbraco.Controller", Controller);
    })();
</pre>

@param {array} content (<code>binding</code>): Array of content items.
@param {array=} contentProperties (<code>binding</code>): Array of content item properties to include in the item. If left empty the item will only show the item icon and name.
@param {callback=} onClick (<code>binding</code>): Callback method to handle click events on the content item.
    <h3>The callback returns:</h3>
    <ul>
        <li><code>item</code>: The clicked item</li>
    </ul>
@param {callback=} onSelect (<code>binding</code>): Callback method to handle click events on the checkmark icon.
    <h3>The callback returns:</h3>
    <ul>
        <li><code>item</code>: The selected item</li>
        <li><code>$event</code>: The select event</li>
        <li><code>$index</code>: The item index</li>
    </ul>
**/

(function() {
   'use strict';

   function ContentGridDirective() {

      function link(scope, el, attr, ctrl) {

         scope.clickItem = function(item) {
            if(scope.onClick) {
               scope.onClick(item);
            }
         };

         scope.selectItem = function(item, $event, $index) {
            if(scope.onSelect) {
               scope.onSelect(item, $event, $index);
               $event.stopPropagation();
            }
         };

      }

      var directive = {
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/umb-content-grid.html',
         scope: {
            content: '=',
            contentProperties: "=",
            onSelect: '=',
            onClick: "="
         },
         link: link
      };

      return directive;
   }

   angular.module('umbraco.directives').directive('umbContentGrid', ContentGridDirective);

})();
