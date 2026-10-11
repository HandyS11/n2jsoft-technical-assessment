Feature: List users

  Scenario: Listing returns every user sorted by last name then first name
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
      | Albert     | Leblanc   |
    When I list the users
    Then the response status is 200
    And the users listed are
      | Name           |
      | Marc Assin     |
      | Albert Leblanc |
      | Juste Leblanc  |

  Scenario: Listing returns no user when none exists
    When I list the users
    Then the response status is 200
    And no user is listed

  Scenario: Listing returns the requested page and the total number of users
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
      | Albert     | Leblanc   |
    When I list page 2 of the users with 2 users per page
    Then the response status is 200
    And the users listed are
      | Name          |
      | Juste Leblanc |
    And the listing reports 3 users in total

  Scenario Outline: Listing rejects an invalid page
    When I request "/v1/users?<query>"
    Then the response is a problem with status 400

    Examples:
      | query          |
      | page=0         |
      | pageSize=0     |
      | pageSize=101   |
      | isActive=maybe |

  Scenario: Listing shows each user's city, monthly quota and status, but not deleted users
    Given the following users exist
      | First name | Last name | City  | Monthly quota | Status   |
      | Juste      | Leblanc   | Paris | 10            | Active   |
      | Marc       | Assin     | Lyon  | 5             | Inactive |
      | Albert     | Leblanc   | Lille | 3             | Deleted  |
    When I list the users
    Then the response status is 200
    And the users listed with their details are
      | Name          | City  | Monthly quota | Status   |
      | Marc Assin    | Lyon  | 5             | Inactive |
      | Juste Leblanc | Paris | 10            | Active   |
    And the listing reports 2 users in total

  Scenario Outline: Listing only the active, or only the inactive, users
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | Active   |
      | Marc       | Assin     | Inactive |
      | Albert     | Leblanc   | Deleted  |
    When I request "/v1/users?isActive=<isActive>"
    Then the response status is 200
    And the users listed are
      | Name   |
      | <name> |
    And the listing reports 1 users in total

    Examples:
      | isActive | name          |
      | true     | Juste Leblanc |
      | false    | Marc Assin    |

  Scenario: Reading the active users slice by slice returns each of them once, in name order
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | Active   |
      | Marc       | Assin     | Active   |
      | Albert     | Leblanc   | Active   |
      | Zoé        | Zola      | Inactive |
      | Paul       | Abel      | Deleted  |
    When I read the active users 2 at a time
    Then the active users read are
      | Name           |
      | Marc Assin     |
      | Albert Leblanc |
      | Juste Leblanc  |

  Scenario Outline: A user <change> between two slices makes the next one neither skip nor repeat the others
    Given the following users exist
      | First name | Last name | Status   |
      | Marc       | Assin     | Active   |
      | Albert     | Leblanc   | Active   |
      | Juste      | Leblanc   | Active   |
      | Zoé        | Zola      | Active   |
      | Paul       | Abel      | Inactive |
    When I read the first 2 active users
    And I <action> the user "<user>"
    And I read the next 2 active users
    Then the active users read are
      | Name           |
      | Marc Assin     |
      | Albert Leblanc |
      | Juste Leblanc  |
      | Zoé Zola       |

    Examples:
      | change                      | action     | user       |
      | already read is deactivated | deactivate | Marc Assin |
      | sorted before is activated  | activate   | Paul Abel  |

  Scenario Outline: Reading the active users rejects an invalid slice
    When I request "/v1/users/active?<query>"
    Then the response is a problem with status 400

    Examples:
      | query               |
      | pageSize=0          |
      | pageSize=101        |
      | after=not-a-cursor  |
      | after=bnVsbA        |
